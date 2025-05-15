using API.Controllers;
using API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace API.Tests.Controllers
{
    public class UserDecentralizationControllerTests
    {
        private readonly Mock<UserManager<AppUser>> _mockUserManager;
        private readonly UserDecentralizationController _controller;

        public UserDecentralizationControllerTests()
        {
            // Setup mock for UserManager
            var userStoreMock = new Mock<IUserStore<AppUser>>();
            _mockUserManager = new Mock<UserManager<AppUser>>(
                userStoreMock.Object, null, null, null, null, null, null, null, null);

            // Initialize controller with mocks
            _controller = new UserDecentralizationController(_mockUserManager.Object);
        }

        #region GetAllListRole Tests

        [Fact]
        public async Task TC_UDEC_01_GetAllListRole_ReturnsUserRoles()
        {
            // Arrange
            var testUser = new AppUser
            {
                Id = "user123",
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "Nguyễn",
                LastName = "Văn A"
            };

            var expectedRoles = new List<string> { "Admin", "User" };

            _mockUserManager.Setup(m => m.GetRolesAsync(It.IsAny<AppUser>()))
                .ReturnsAsync(expectedRoles);

            // Act
            var result = await _controller.GetAllListRole(testUser);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("Admin", result[0]);
            Assert.Equal("User", result[1]);

            // Verify UserManager.GetRolesAsync was called with correct user
            _mockUserManager.Verify(m => m.GetRolesAsync(testUser), Times.Once);
        }

        [Fact]
        public async Task TC_UDEC_02_GetAllListRole_ReturnsEmptyList_WhenUserHasNoRoles()
        {
            // Arrange
            var testUser = new AppUser
            {
                Id = "user456",
                UserName = "newuser",
                Email = "newuser@example.com",
                FirstName = "Trần",
                LastName = "Thị B"
            };

            _mockUserManager.Setup(m => m.GetRolesAsync(It.IsAny<AppUser>()))
                .ReturnsAsync(new List<string>());

            // Act
            var result = await _controller.GetAllListRole(testUser);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);

            // Verify UserManager.GetRolesAsync was called with correct user
            _mockUserManager.Verify(m => m.GetRolesAsync(testUser), Times.Once);
        }

        #endregion

        #region CreateNewRole Tests

        [Fact]
        public async Task TC_UDEC_03_CreateNewRole_AddsRoleToUser()
        {
            // Arrange
            var testUser = new AppUser
            {
                Id = "user123",
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "Nguyễn",
                LastName = "Văn A"
            };

            var roleToAdd = "Manager";

            _mockUserManager.Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.CreateNewRole(testUser, roleToAdd);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Verify UserManager.AddToRoleAsync was called with correct parameters
            _mockUserManager.Verify(m => m.AddToRoleAsync(testUser, roleToAdd), Times.Once);
        }

        [Fact]
        public async Task TC_UDEC_04_CreateNewRole_HandlesIdentityError()
        {
            // Arrange
            var testUser = new AppUser
            {
                Id = "user123",
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "Nguyễn",
                LastName = "Văn A"
            };

            var roleToAdd = "SuperAdmin";
            var identityErrors = new IdentityError[] { new IdentityError { Code = "RoleNotFound", Description = "Role not found." } };

            _mockUserManager.Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(identityErrors));

            // Act
            var result = await _controller.CreateNewRole(testUser, roleToAdd);

            // Assert
            // Controller always returns Ok(), even if the AddToRoleAsync fails
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Verify UserManager.AddToRoleAsync was called with correct parameters
            _mockUserManager.Verify(m => m.AddToRoleAsync(testUser, roleToAdd), Times.Once);
        }

        [Fact]
        public async Task TC_UDEC_05_CreateNewRole_WithNullUser()
        {
            // Arrange
            AppUser nullUser = null;
            var roleToAdd = "User";

            _mockUserManager.Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            // Act & Assert
            await Assert.ThrowsAsync<System.NullReferenceException>(async () =>
                await _controller.CreateNewRole(nullUser, roleToAdd));

            // Verify UserManager.AddToRoleAsync was not called
            _mockUserManager.Verify(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
        }

        #endregion
    }
}