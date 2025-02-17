﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BackendFramework.Helper;
using BackendFramework.Interfaces;
using BackendFramework.Models;

namespace Backend.Tests.Mocks
{
    public class MergeBlacklistRepositoryMock : IMergeBlacklistRepository
    {
        private readonly List<MergeBlacklistEntry> _mergeBlacklist;

        public MergeBlacklistRepositoryMock()
        {
            _mergeBlacklist = new List<MergeBlacklistEntry>();
        }

        public Task<List<MergeBlacklistEntry>> GetAllEntries(string projectId, string? userId = null)
        {
            var cloneList = _mergeBlacklist.Select(e => e.Clone()).ToList();
            var enumerable = userId is null ?
                cloneList.Where(e => e.ProjectId == projectId) :
                cloneList.Where(e => e.ProjectId == projectId && e.UserId == userId);
            return Task.FromResult(enumerable.ToList());
        }

        public Task<MergeBlacklistEntry?> GetEntry(string projectId, string entryId)
        {
            try
            {
                var foundMergeBlacklist = _mergeBlacklist.Single(entry => entry.Id == entryId);
                return Task.FromResult<MergeBlacklistEntry?>(foundMergeBlacklist.Clone());
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult<MergeBlacklistEntry?>(null);
            }
        }

        public Task<MergeBlacklistEntry> Create(MergeBlacklistEntry blacklistEntry)
        {
            blacklistEntry.Id = Guid.NewGuid().ToString();
            _mergeBlacklist.Add(blacklistEntry.Clone());
            return Task.FromResult(blacklistEntry.Clone());
        }

        public Task<bool> DeleteAllEntries(string projectId)
        {
            _mergeBlacklist.Clear();
            return Task.FromResult(true);
        }

        public Task<bool> Delete(string projectId, string entryId)
        {
            var foundMergeBlacklist = _mergeBlacklist.Single(entry => entry.Id == entryId);
            return Task.FromResult(_mergeBlacklist.Remove(foundMergeBlacklist));
        }

        public Task<ResultOfUpdate> Update(MergeBlacklistEntry blacklistEntry)
        {
            var foundEntry = _mergeBlacklist.Single(
                e => e.ProjectId == blacklistEntry.ProjectId && e.Id == blacklistEntry.Id);
            var success = _mergeBlacklist.Remove(foundEntry);
            if (!success)
            {
                return Task.FromResult(ResultOfUpdate.NotFound);
            }

            _mergeBlacklist.Add(blacklistEntry.Clone());
            return Task.FromResult(ResultOfUpdate.Updated);
        }
    }
}
