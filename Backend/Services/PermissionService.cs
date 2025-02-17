﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BackendFramework.Helper;
using BackendFramework.Interfaces;
using BackendFramework.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;

namespace BackendFramework.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IUserRepository _userRepo;
        private readonly IUserRoleRepository _userRoleRepo;

        // TODO: This appears intrinsic to mongodb implementation and is brittle.
        private const int ProjIdLength = 24;
        private const string ProjPath = "projects/";

        public PermissionService(IUserRepository userRepo, IUserRoleRepository userRoleRepo)
        {
            _userRepo = userRepo;
            _userRoleRepo = userRoleRepo;
        }

        private static SecurityToken GetJwt(HttpContext request)
        {
            // Get authorization header (i.e. JWT token)
            var jwtToken = request.Request.Headers["Authorization"].ToString();

            // Remove "Bearer " from beginning of token
            var token = jwtToken.Split(" ")[1];

            // Parse JWT for project permissions
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token);

            return jsonToken;
        }

        public bool IsUserIdAuthorized(HttpContext request, string userId)
        {
            var currentUserId = GetUserId(request);
            return userId == currentUserId;
        }

        /// <summary>
        /// Checks whether the current user is authorized.
        /// </summary>
        public bool IsCurrentUserAuthorized(HttpContext request)
        {
            var userId = GetUserId(request);
            return IsUserIdAuthorized(request, userId);
        }

        private static List<ProjectPermissions> GetProjectPermissions(HttpContext request)
        {
            var jsonToken = GetJwt(request);
            var userRoleInfo = ((JwtSecurityToken)jsonToken).Payload["UserRoleInfo"].ToString();
            // If unable to parse permissions, return empty permissions.
            if (userRoleInfo is null)
            {
                return new List<ProjectPermissions>();
            }

            var permissions = JsonSerializer.Deserialize<List<ProjectPermissions>>(userRoleInfo);
            return permissions ?? new List<ProjectPermissions>();
        }

        public async Task<bool> IsSiteAdmin(HttpContext request)
        {
            var userId = GetUserId(request);
            var user = await _userRepo.GetUser(userId);
            if (user is null)
            {
                return false;
            }
            return user.IsAdmin;
        }

        /// <remarks>
        /// This method magically looks up the Project ID by inspecting the route.
        /// It is not suitable for any routes that do not contain ...projects/PROJECT_ID... in the route.
        /// </remarks>
        public async Task<bool> HasProjectPermission(HttpContext request, Permission permission)
        {
            var user = await _userRepo.GetUser(GetUserId(request));
            if (user is null)
            {
                return false;
            }

            // Database administrators implicitly possess all permissions.
            if (user.IsAdmin)
            {
                return true;
            }

            // Retrieve project ID from HTTP request
            // TODO: This method of retrieving the project ID is brittle, should use regex or some other method.
            var pathString = request.Request.Path.ToString();
            var projIdIndex = pathString.LastIndexOf(ProjPath, StringComparison.OrdinalIgnoreCase) + ProjPath.Length;
            if (projIdIndex + ProjIdLength > pathString.Length)
            {
                // If there is no project ID, do not allow changes
                return false;
            }
            var projectId = pathString.Substring(projIdIndex, ProjIdLength);

            return HasProjectPermission(request, permission, projectId);
        }

        private static bool HasProjectPermission(HttpContext request, Permission permission, string projectId)
        {
            // Retrieve JWT token from HTTP request and convert to object
            var projectPermissionsList = GetProjectPermissions(request);

            // Assert that the user has permission for this function
            foreach (var projectPermissions in projectPermissionsList)
            {
                if (projectPermissions.ProjectId != projectId)
                {
                    continue;
                }

                if (projectPermissions.Permissions.Contains(permission))
                {
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> ContainsProjectRole(HttpContext request, Role role, string projectId)
        {
            var user = await _userRepo.GetUser(GetUserId(request));
            if (user is null)
            {
                return false;
            }

            // Database administrators implicitly possess all permissions.
            if (user.IsAdmin)
            {
                return true;
            }

            // Retrieve JWT token from HTTP request and convert to object
            var projectPermissionsList = GetProjectPermissions(request);

            // Assert that the user has all permissions in the specified role
            foreach (var projPermissions in projectPermissionsList)
            {
                if (projPermissions.ProjectId != projectId)
                {
                    continue;
                }
                return ProjectRole.RolePermissions(role).All(p => projPermissions.Permissions.Contains(p));
            }
            return false;
        }

        public async Task<bool> IsViolationEdit(HttpContext request, string userEditId, string projectId)
        {
            var userId = GetUserId(request);
            var user = await _userRepo.GetUser(userId);
            if (user is null)
            {
                return true;
            }

            return user.WorkedProjects[projectId] != userEditId;
        }

        /// <summary>Retrieve the User ID from the JWT in a request. </summary>
        /// <exception cref="InvalidJwtTokenException"> Throws when null userId extracted from token. </exception>
        public string GetUserId(HttpContext request)
        {
            var jsonToken = GetJwt(request);
            var userId = ((JwtSecurityToken)jsonToken).Payload["UserId"].ToString();
            if (userId is null)
            {
                throw new InvalidJwtTokenException();
            }

            return userId;
        }

        /// <summary> Confirms login credentials are valid. </summary>
        /// <returns> User when credentials are correct, null otherwise. </returns>
        public async Task<User?> Authenticate(string username, string password)
        {
            // Fetch the stored user.
            var user = await _userRepo.GetUserByUsername(username, false);

            // Return null if user with specified username not found.
            if (user is null)
            {
                return null;
            }

            // Extract the bytes from encoded password.
            var hashedPassword = Convert.FromBase64String(user.Password);

            // If authentication is successful, generate jwt token.
            return PasswordHash.ValidatePassword(hashedPassword, password) ? await MakeJwt(user) : null;
        }

        public async Task<User?> MakeJwt(User user)
        {
            const int hoursUntilExpires = 4;
            var tokenHandler = new JwtSecurityTokenHandler();
            var secretKey = Environment.GetEnvironmentVariable("COMBINE_JWT_SECRET_KEY")!;
            var key = Encoding.ASCII.GetBytes(secretKey);

            // Fetch the projects Id and the roles for each Id
            var projectPermissionMap = new List<ProjectPermissions>();

            foreach (var (projectRoleKey, projectRoleValue) in user.ProjectRoles)
            {
                // Convert each userRoleId to its respective role and add to the mapping
                var userRole = await _userRoleRepo.GetUserRole(projectRoleKey, projectRoleValue);
                if (userRole is null)
                {
                    return null;
                }

                var permissions = ProjectRole.RolePermissions(userRole.Role);
                var validEntry = new ProjectPermissions(projectRoleKey, permissions);
                projectPermissionMap.Add(validEntry);
            }

            var claimString = projectPermissionMap.ToJson();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("UserId", user.Id),
                    new Claim("UserRoleInfo", claimString)
                }),

                Expires = DateTime.UtcNow.AddHours(hoursUntilExpires),

                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);

            // Sanitize user to remove password, avatar path, and old token
            // Then add updated token.
            user.Sanitize();
            user.Token = tokenHandler.WriteToken(token);

            if (await _userRepo.Update(user.Id, user) != ResultOfUpdate.Updated)
            {
                return null;
            }

            return user;
        }

        [Serializable]
        public class InvalidJwtTokenException : Exception
        {
            public InvalidJwtTokenException() { }

            public InvalidJwtTokenException(string msg) : base(msg) { }

            public InvalidJwtTokenException(string msg, Exception innerException) : base(msg, innerException) { }

            protected InvalidJwtTokenException(SerializationInfo info, StreamingContext context)
                : base(info, context) { }
        }
    }

    public class ProjectPermissions
    {
        public ProjectPermissions(string projectId, List<Permission> permissions)
        {
            ProjectId = projectId;
            Permissions = permissions;
        }
        public string ProjectId { get; }
        public List<Permission> Permissions { get; }
    }
}

