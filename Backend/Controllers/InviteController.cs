using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BackendFramework.Interfaces;
using BackendFramework.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BackendFramework.Controllers
{
    [Authorize]
    [Produces("application/json")]
    [Route("v1/invite")]
    public class InviteController : Controller
    {
        private readonly IProjectRepository _projRepo;
        private readonly IUserRepository _userRepo;
        private readonly IInviteService _inviteService;
        private readonly IPermissionService _permissionService;

        public InviteController(
            IInviteService inviteService, IProjectRepository projRepo, IUserRepository userRepo,
            IPermissionService permissionService)
        {
            _projRepo = projRepo;
            _userRepo = userRepo;
            _inviteService = inviteService;
            _permissionService = permissionService;
        }

        /// <summary> Generates invite link and sends email containing link </summary>
        [HttpPut(Name = "EmailInviteToProject")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
        public async Task<IActionResult> EmailInviteToProject([FromBody, BindRequired] EmailInviteData data)
        {
            if (!await _permissionService.HasProjectPermission(HttpContext, Permission.DeleteEditSettingsAndUsers))
            {
                return Forbid();
            }

            var projectId = data.ProjectId;
            if (!await _permissionService.ContainsProjectRole(HttpContext, data.Role, projectId))
            {
                return Forbid();
            }

            var project = await _projRepo.GetProject(projectId);
            if (project is null)
            {
                return NotFound(projectId);
            }

            var linkWithIdentifier = await _inviteService.CreateLinkWithToken(project, data.Role, data.EmailAddress);
            await _inviteService.EmailLink(data.EmailAddress, data.Message, linkWithIdentifier, data.Domain, project);
            return Ok(linkWithIdentifier);
        }

        /// <summary> Validates invite token in url and adds user to project </summary>
        [AllowAnonymous]
        [HttpPut("{projectId}/validate/{token}", Name = "ValidateToken")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EmailInviteStatus))]
        public async Task<IActionResult> ValidateToken(string projectId, string token)
        {
            var project = await _projRepo.GetProject(projectId);
            if (project is null)
            {
                return NotFound(projectId);
            }

            var isTokenValid = false;
            var tokenObj = new EmailInvite();
            foreach (var tok in project.InviteTokens)
            {
                if (tok.Token == token && DateTime.Now < tok.ExpireTime)
                {
                    isTokenValid = true;
                    tokenObj = tok;
                    break;
                }
            }

            var users = await _userRepo.GetAllUsers();
            var currentUser = new User();
            var isUserRegistered = false;
            foreach (var user in users)
            {
                if (user.Email == tokenObj.Email)
                {
                    currentUser = user;
                    isUserRegistered = true;
                    break;
                }
            }

            var status = new EmailInviteStatus(isTokenValid, isUserRegistered);
            if (isTokenValid && !isUserRegistered)
            {
                return Ok(status);
            }
            if (isTokenValid && isUserRegistered
                                  && !currentUser.ProjectRoles.ContainsKey(projectId)
                                  && await _inviteService.RemoveTokenAndCreateUserRole(project, currentUser, tokenObj))
            {
                return Ok(status);
            }
            return Ok(new EmailInviteStatus(false, false));
        }

        /// <remarks>
        /// This is used in a [FromBody] serializer, so its attributes cannot be set to readonly.
        /// </remarks>
        public class EmailInviteData
        {
            [Required]
            public string EmailAddress { get; set; }
            [Required]
            public string Message { get; set; }
            [Required]
            public string ProjectId { get; set; }
            [Required]
            public Role Role { get; set; }
            [Required]
            public string Domain { get; set; }

            public EmailInviteData()
            {
                EmailAddress = "";
                Message = "";
                ProjectId = "";
                Role = Role.Harvester;
                Domain = "";
            }
        }

        /// <remarks>
        /// This is used in an OpenAPI return value serializer, so its attributes must be defined as properties.
        /// </remarks>
        public class EmailInviteStatus
        {
            [Required]
            public bool IsTokenValid { get; set; }
            [Required]
            public bool IsUserRegistered { get; set; }

            public EmailInviteStatus(bool isTokenValid, bool isUserRegistered)
            {
                IsTokenValid = isTokenValid;
                IsUserRegistered = isUserRegistered;
            }
        }
    }
}
