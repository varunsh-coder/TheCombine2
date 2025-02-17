using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using BackendFramework.Helper;
using BackendFramework.Interfaces;
using BackendFramework.Models;

namespace BackendFramework.Services
{
    /// <summary> More complex functions and application logic for <see cref="Word"/>s </summary>
    public class MergeService : IMergeService
    {
        private readonly IMergeBlacklistRepository _mergeBlacklistRepo;
        private readonly IWordRepository _wordRepo;
        private readonly IWordService _wordService;

        public MergeService(
            IMergeBlacklistRepository mergeBlacklistRepo, IWordRepository wordRepo, IWordService wordService)
        {
            _mergeBlacklistRepo = mergeBlacklistRepo;
            _wordRepo = wordRepo;
            _wordService = wordService;
        }

        /// <summary> Prepares a merge parent to be added to the database. </summary>
        /// <returns> Word to add. </returns>
        private async Task<Word> MergePrepParent(string projectId, MergeWords mergeWords)
        {
            var parent = mergeWords.Parent.Clone();
            parent.ProjectId = projectId;
            parent.History = new List<string>();

            // Add child to history.
            foreach (var childSource in mergeWords.Children)
            {
                parent.History.Add(childSource.SrcWordId);
                if (childSource.GetAudio)
                {
                    var child = await _wordRepo.GetWord(projectId, childSource.SrcWordId);
                    if (child is null)
                    {
                        throw new KeyNotFoundException($"Unable to locate word: ${childSource.SrcWordId}");
                    }
                    parent.Audio.AddRange(child.Audio);
                }
            }

            // Remove duplicates.
            parent.Audio = parent.Audio.Distinct().ToList();
            parent.History = parent.History.Distinct().ToList();

            // Clear fields to be automatically regenerated.
            parent.Id = "";
            parent.Modified = "";

            return parent;
        }

        /// <summary> Deletes all the merge children from the frontier. </summary>
        /// <returns> Number of words deleted. </returns>
        private async Task<long> MergeDeleteChildren(string projectId, MergeWords mergeWords)
        {
            var childIds = mergeWords.Children.Select(c => c.SrcWordId).ToList();
            return await _wordRepo.DeleteFrontier(projectId, childIds);
        }

        /// <summary>
        /// Given a list of MergeWords, preps the words to be added, removes the children
        /// from the frontier, and adds the new words to the database.
        /// </summary>
        /// <returns> List of new words added. </returns>
        public async Task<List<Word>> Merge(string projectId, List<MergeWords> mergeWordsList)
        {
            var keptWords = mergeWordsList.Where(m => !m.DeleteOnly);
            var newWords = keptWords.Select(m => MergePrepParent(projectId, m).Result).ToList();
            await Task.WhenAll(mergeWordsList.Select(m => MergeDeleteChildren(projectId, m)));
            return await _wordRepo.Create(newWords);
        }

        /// <summary> Undo merge </summary>
        /// <returns> True if merge was successfully undone </returns>
        public async Task<bool> UndoMerge(string projectId, MergeUndoIds ids)
        {
            foreach (var parentId in ids.ParentIds)
            {
                var parentWord = (await _wordRepo.GetWord(projectId, parentId))?.Clone();
                if (parentWord is null)
                {
                    return false;
                }
            }

            var childWords = new List<Word>();
            foreach (var childId in ids.ChildIds)
            {
                var childWord = await _wordRepo.GetWord(projectId, childId);
                if (childWord is null)
                {
                    return false;
                }
                childWords.Add(childWord);
            }


            // Separate foreach loop for deletion to prevent partial undos
            foreach (var parentId in ids.ParentIds)
            {
                await _wordService.DeleteFrontierWord(projectId, parentId);
            }

            await _wordRepo.AddFrontier(childWords);
            return true;
        }

        /// <summary> Adds a List of wordIds to MergeBlacklist of specified <see cref="Project"/>. </summary>
        /// <exception cref="InvalidBlacklistEntryException"> Throws when wordIds has count less than 2. </exception>
        /// <returns> The <see cref="MergeBlacklistEntry"/> created. </returns>
        public async Task<MergeBlacklistEntry> AddToMergeBlacklist(
            string projectId, string userId, List<string> wordIds)
        {
            if (wordIds.Count < 2)
            {
                throw new InvalidBlacklistEntryException("Cannot blacklist a list of fewer than 2 wordIds.");
            }
            // When we switch from individual to common blacklist, the userId argument here should be removed.
            var blacklist = await _mergeBlacklistRepo.GetAllEntries(projectId, userId);
            foreach (var entry in blacklist)
            {
                if (entry.WordIds.All(wordIds.Contains))
                {
                    await _mergeBlacklistRepo.Delete(projectId, entry.Id);
                }
            }
            var newEntry = new MergeBlacklistEntry { ProjectId = projectId, UserId = userId, WordIds = wordIds };
            return await _mergeBlacklistRepo.Create(newEntry);
        }

        /// <summary> Check if List of wordIds is in MergeBlacklist for specified <see cref="Project"/>. </summary>
        /// <exception cref="InvalidBlacklistEntryException"> Throws when wordIds has count less than 2. </exception>
        /// <returns> A bool, true if in the blacklist. </returns>
        public async Task<bool> IsInMergeBlacklist(string projectId, List<string> wordIds, string? userId = null)
        {
            if (wordIds.Count < 2)
            {
                throw new InvalidBlacklistEntryException("Cannot blacklist a list of fewer than 2 wordIds.");
            }
            var blacklist = await _mergeBlacklistRepo.GetAllEntries(projectId, userId);
            foreach (var entry in blacklist)
            {
                if (wordIds.All(entry.WordIds.Contains))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Update merge blacklist for specified <see cref="Project"/> to current frontier.
        /// Remove from all blacklist entries any ids for words no longer in the frontier
        /// and delete entries that no longer have at least two wordIds.
        /// </summary>
        /// <returns> Number of <see cref="MergeBlacklistEntry"/>s updated. </returns>
        public async Task<int> UpdateMergeBlacklist(string projectId)
        {
            var oldBlacklist = await _mergeBlacklistRepo.GetAllEntries(projectId);
            if (oldBlacklist.Count == 0)
            {
                return 0;
            }
            var frontierWordIds = (await _wordRepo.GetFrontier(projectId)).Select(word => word.Id);
            var updateCount = 0;
            foreach (var entry in oldBlacklist)
            {
                var newIds = entry.WordIds.Where(id => frontierWordIds.Contains(id)).ToList();
                if (newIds.Count == entry.WordIds.Count)
                {
                    continue;
                }

                updateCount++;
                if (newIds.Count > 1)
                {
                    entry.WordIds = newIds;
                    await _mergeBlacklistRepo.Update(entry);
                }
                else
                {
                    await _mergeBlacklistRepo.Delete(projectId, entry.Id);
                }
            }
            return updateCount;
        }

        /// <summary>
        /// Get Lists of potential duplicate <see cref="Word"/>s in specified <see cref="Project"/>'s frontier.
        /// </summary>
        public async Task<List<List<Word>>> GetPotentialDuplicates(
            string projectId, int maxInList, int maxLists, string? userId = null)
        {
            var dupFinder = new DuplicateFinder(maxInList, maxLists, 3);

            // First pass, only look for words with identical vernacular.
            var collection = await _wordRepo.GetFrontier(projectId);
            var wordLists = await dupFinder.GetIdenticalVernWords(
                collection, wordIds => IsInMergeBlacklist(projectId, wordIds, userId));

            // If no such sets found, look for similar words.
            if (wordLists.Count == 0)
            {
                collection = await _wordRepo.GetFrontier(projectId);
                wordLists = await dupFinder.GetSimilarWords(
                    collection, wordIds => IsInMergeBlacklist(projectId, wordIds, userId));
            }

            return wordLists;
        }

        [Serializable]
        public class InvalidBlacklistEntryException : Exception
        {
            public InvalidBlacklistEntryException() { }

            public InvalidBlacklistEntryException(string message) : base(message) { }

            protected InvalidBlacklistEntryException(SerializationInfo info, StreamingContext context)
                : base(info, context) { }
        }
    }
}
