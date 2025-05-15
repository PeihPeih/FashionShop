using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helpers;
using API.Models;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace API.Tests.Controllers
{
    public class AccountsControllerTests : IDisposable
    {
        private readonly DPContext _context;
        private readonly DbContextOptions<DPContext> _options;
        private readonly Mock<UserManager<AppUser>> _mockUserManager;
        private readonly Mock<IMapper> _mockMapper;
        private readonly AccountsController _controller;

        public AccountsControllerTests()
        {
            // Setup in-memory database
            _options = new DbContextOptionsBuilder<DPContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new DPContext(_options);

            // Setup mocks
            var userStoreMock = new Mock<IUserStore<AppUser>>();
            _mockUserManager = new Mock<UserManager<AppUser>>(
                userStoreMock.Object, null, null, null, null, null, null, null, null);

            _mockMapper = new Mock<IMapper>();

            _controller = new AccountsController(
                _mockUserManager.Object,
                _mockMapper.Object,
                _context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Registration Tests

        [Fact]
        public async Task TC_ACC_01_Registration_Success()
        {
            // Arrange
            var formFile = new Mock<IFormFile>();

            var registrationData = new RegistrationViewModel
            {
                Email = "test@example.com",
                Password = "Test@123",
                FirstName = "John",
                LastName = "Doe",
                SDT = "0987654321",
                DiaChi = "123 Nguyen Hue, Ho Chi Minh City"
            };

            var newAppUser = new AppUser
            {
                Id = "user123",
                UserName = "test@example.com",
                Email = "test@example.com",
                FirstName = "John",
                LastName = "Doe",
                SDT = "0987654321",
                DiaChi = "123 Nguyen Hue, Ho Chi Minh City"
            };

            _mockMapper.Setup(m => m.Map<AppUser>(registrationData))
                .Returns(newAppUser);

            _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<AppUser>(), registrationData.Password))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.Post(registrationData);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Verify database was updated correctly
            var dbUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Id == "user123");
            Assert.NotNull(dbUser);
        }

        [Fact]
        public async Task TC_ACC_02_Registration_Invalid_Model()
        {
            // Arrange
            var registrationData = new RegistrationViewModel
            {
                // Missing required fields
                Email = "",
                Password = "123", // Too short
                FirstName = "John"
                // Missing other fields
            };

            _controller.ModelState.AddModelError("Email", "Email is required");
            _controller.ModelState.AddModelError("Password", "Password must be at least 6 characters");

            // Act
            var result = await _controller.Post(registrationData);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            // Verify no user was added to database
            var userCount = await _context.AppUsers.CountAsync();
            Assert.Equal(0, userCount);
        }

        [Fact]
        public async Task TC_ACC_03_Registration_User_Creation_Failed()
        {
            // Arrange
            var registrationData = new RegistrationViewModel
            {
                Email = "existing@example.com",
                Password = "Test@123",
                FirstName = "John",
                LastName = "Doe",
                SDT = "0987654321",
                DiaChi = "123 Nguyen Hue, Ho Chi Minh City"
            };

            var newAppUser = new AppUser
            {
                Id = "user123",
                UserName = "existing@example.com",
                Email = "existing@example.com"
            };

            _mockMapper.Setup(m => m.Map<AppUser>(registrationData))
                .Returns(newAppUser);

            // Setup UserManager to return failure (e.g., email already exists)
            var identityErrors = new[]
            {
                new IdentityError { Code = "DuplicateEmail", Description = "Email already exists" }
            };
            _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<AppUser>(), registrationData.Password))
                .ReturnsAsync(IdentityResult.Failed(identityErrors));

            // Act
            var result = await _controller.Post(registrationData);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);

            // Verify no user was added to database
            var userCount = await _context.AppUsers.CountAsync();
            Assert.Equal(0, userCount);
        }

        #endregion

        #region Update Profile Tests

        [Fact]
        public async Task TC_ACC_04_Update_Profile_Success()
        {
            // Arrange
            var userId = "user123";
            var updateModel = new UpdateUserProfile
            {
                FirstName = "Jane",
                LastName = "Smith",
                SDT = "0123456789",
                DiaChi = "456 Le Loi, District 1, Ho Chi Minh City",
                Password = "NewPass@123"
            };

            // Add test user to DB
            var initialUser = new AppUser
            {
                Id = userId,
                UserName = "test@example.com",
                Email = "test@example.com",
                FirstName = "John",
                LastName = "Doe",
                SDT = "0987654321",
                DiaChi = "123 Nguyen Hue, Ho Chi Minh City",
                PasswordHash = "OldHashedPassword"
            };
            await _context.AppUsers.AddAsync(initialUser);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Put(updateModel, userId);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Verify user was updated in database
            var updatedUser = await _context.AppUsers.FindAsync(userId);
            Assert.NotNull(updatedUser);
            Assert.Equal(updateModel.FirstName, updatedUser.FirstName);
            Assert.Equal(updateModel.LastName, updatedUser.LastName);
            Assert.Equal(updateModel.SDT, updatedUser.SDT);
            Assert.Equal(updateModel.DiaChi, updatedUser.DiaChi);
            Assert.Equal(updateModel.Password, updatedUser.PasswordHash); // Note: In real app, this would be hashed
        }

        [Fact]
        public async Task TC_ACC_05_Update_Profile_User_Not_Found()
        {
            // Arrange
            var nonExistentUserId = "nonexistent";
            var updateModel = new UpdateUserProfile
            {
                FirstName = "Jane",
                LastName = "Smith",
                SDT = "0123456789",
                DiaChi = "456 Le Loi, District 1, Ho Chi Minh City",
                Password = "NewPass@123"
            };

            // Act & Assert
            // This should throw an exception since the user doesn't exist
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
                await _controller.Put(updateModel, nonExistentUserId));

            // Verify database wasn't changed
            var userCount = await _context.AppUsers.CountAsync();
            Assert.Equal(0, userCount);
        }

        [Fact]
        public async Task TC_ACC_06_Update_Profile_Partial_Update()
        {
            // Arrange
            var userId = "user123";
            var updateModel = new UpdateUserProfile
            {
                FirstName = "Jane",
                LastName = "Smith",
                // SDT and DiaChi left as default/null
                Password = "NewPass@123"
            };

            // Add test user to DB
            var initialUser = new AppUser
            {
                Id = userId,
                UserName = "test@example.com",
                Email = "test@example.com",
                FirstName = "John",
                LastName = "Doe",
                SDT = "0987654321",
                DiaChi = "123 Nguyen Hue, Ho Chi Minh City",
                PasswordHash = "OldHashedPassword"
            };
            await _context.AppUsers.AddAsync(initialUser);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Put(updateModel, userId);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Verify only specified fields were updated
            var updatedUser = await _context.AppUsers.FindAsync(userId);
            Assert.NotNull(updatedUser);
            Assert.Equal(updateModel.FirstName, updatedUser.FirstName);
            Assert.Equal(updateModel.LastName, updatedUser.LastName);
            Assert.Equal(null, updatedUser.SDT); // Should be set to null/default
            Assert.Equal(null, updatedUser.DiaChi); // Should be set to null/default
            Assert.Equal(updateModel.Password, updatedUser.PasswordHash);
        }

        #endregion
    }
}