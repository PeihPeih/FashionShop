using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Controllers;
using API.Data;
using API.Models;
using Xunit;
using System.Threading;

namespace API.Test
{
    public class UserManagersControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly Mock<UserManager<AppUser>> _userManagerMock;
        private readonly UserManagersController _controller;

        public UserManagersControllerTests() : base()
        {
            // Khởi tạo InMemoryDatabase với GUID để tránh xung đột
            _context = new DPContext(_options);

            // Tạo mock cho IUserStore<AppUser>
            var storeMock = new Mock<IUserStore<AppUser>>();
            storeMock.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string userId, CancellationToken token) =>
                         _context.AppUsers.FirstOrDefault(u => u.Id == userId));

            // Tạo các dependency khác của UserManager
            var optionsMock = new Mock<IOptions<IdentityOptions>>();
            optionsMock.Setup(x => x.Value).Returns(new IdentityOptions());
            var passwordHasherMock = new Mock<IPasswordHasher<AppUser>>();
            var userValidators = new List<IUserValidator<AppUser>> { new Mock<IUserValidator<AppUser>>().Object };
            var passwordValidators = new List<IPasswordValidator<AppUser>> { new Mock<IPasswordValidator<AppUser>>().Object };
            var keyNormalizerMock = new Mock<ILookupNormalizer>();
            var errorsMock = new Mock<IdentityErrorDescriber>();
            var servicesMock = new Mock<IServiceProvider>();
            var loggerMock = new Mock<ILogger<UserManager<AppUser>>>();

            // Khởi tạo UserManager mock với các dependency
            _userManagerMock = new Mock<UserManager<AppUser>>(
                storeMock.Object,
                optionsMock.Object,
                passwordHasherMock.Object,
                userValidators,
                passwordValidators,
                keyNormalizerMock.Object,
                errorsMock.Object,
                servicesMock.Object,
                loggerMock.Object
            );

            // Mock UserManager.Users để trả về dữ liệu từ _context.AppUsers
            var usersQueryable = _context.AppUsers.AsQueryable();
            _userManagerMock.Setup(u => u.Users).Returns(usersQueryable);

            // Mock các phương thức khác nếu cần
            _userManagerMock.Setup(u => u.GetUsersInRoleAsync(It.IsAny<string>())).ReturnsAsync(new List<AppUser>());
            _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

            // Khởi tạo controller
            _controller = new UserManagersController(_context, _userManagerMock.Object);

            // Không cần Cleanup nếu dùng InMemoryDatabase
        }

        // UM01: Kiểm tra lấy danh sách người dùng trả về đúng khi có dữ liệu
        [Fact]
        public async Task Gets_ShouldReturnUsers_WhenDataExists()
        {
            var user1 = new AppUser { Id = "1", FirstName = "User1", Email = "user1@example.com" };
            var user2 = new AppUser { Id = "2", FirstName = "User2", Email = "user2@example.com" };
            _context.AppUsers.AddRange(user1, user2);
            await _context.SaveChangesAsync();

            var result = await _controller.Gets();
            var actionResult = Assert.IsType<ActionResult<IEnumerable<AppUser>>>(result);
            var users = Assert.IsType<List<AppUser>>(actionResult.Value);
            Assert.Equal(2, users.Count);
            Assert.Contains(users, u => u.FirstName == "User1");
            Assert.Contains(users, u => u.FirstName == "User2");
        }

        // UM02: Kiểm tra lấy danh sách người dùng trả về rỗng khi không có dữ liệu
        [Fact]
        public async Task Gets_ShouldReturnEmpty_WhenNoData()
        {
            var result = await _controller.Gets();
            var actionResult = Assert.IsType<ActionResult<IEnumerable<AppUser>>>(result);
            var users = Assert.IsType<List<AppUser>>(actionResult.Value);
            Assert.Empty(users);
        }

        // UM03: Kiểm tra lấy danh sách người dùng trả về BadRequest khi có ngoại lệ
        [Fact]
        public async Task Gets_ShouldReturnBadRequest_WhenExceptionThrown()
        {
            var storeMock = new Mock<IUserStore<AppUser>>();
            var optionsMock = new Mock<IOptions<IdentityOptions>>();
            var passwordHasherMock = new Mock<IPasswordHasher<AppUser>>();
            var userValidators = new List<IUserValidator<AppUser>> { new Mock<IUserValidator<AppUser>>().Object };
            var passwordValidators = new List<IPasswordValidator<AppUser>> { new Mock<IPasswordValidator<AppUser>>().Object };
            var keyNormalizerMock = new Mock<ILookupNormalizer>();
            var errorsMock = new Mock<IdentityErrorDescriber>();
            var servicesMock = new Mock<IServiceProvider>();
            var loggerMock = new Mock<ILogger<UserManager<AppUser>>>();

            var userManagerMock = new Mock<UserManager<AppUser>>(
                storeMock.Object,
                optionsMock.Object,
                passwordHasherMock.Object,
                userValidators,
                passwordValidators,
                keyNormalizerMock.Object,
                errorsMock.Object,
                servicesMock.Object,
                loggerMock.Object
            );

            userManagerMock.Setup(u => u.Users).Throws(new Exception("UserManager error"));
            var controllerWithMock = new UserManagersController(_context, userManagerMock.Object);

            var result = await controllerWithMock.Gets();
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}