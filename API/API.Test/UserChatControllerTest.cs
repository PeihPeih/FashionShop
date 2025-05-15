using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helper.SignalR;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace API.Tests.Controllers
{
    public class UserChatsControllerTests : IDisposable
    {
        private readonly DPContext _context;
        private readonly DbContextOptions<DPContext> _options;
        private readonly Mock<IHubContext<BroadcastHub, IHubClient>> _mockHubContext;
        private readonly UserChatsController _controller;
        private readonly Mock<IHubClients<IHubClient>> _mockClients;

        public UserChatsControllerTests()
        {
            // Setup in-memory database
            _options = new DbContextOptionsBuilder<DPContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new DPContext(_options);

            // Setup mocks for SignalR hub
            _mockHubContext = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            _mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClient = new Mock<IHubClient>();

            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(mockClient.Object);
            mockClient.Setup(c => c.BroadcastMessage()).Returns(Task.CompletedTask);

            // Initialize controller with mocks
            _controller = new UserChatsController(_context, _mockHubContext.Object);

            // Setup test data
            SeedDatabase();
        }

        private void SeedDatabase()
        {
            // Add test users
            var users = new List<AppUser>
            {
                new AppUser
                {
                    Id = "user123",
                    UserName = "nguyenvana@example.com",
                    Email = "nguyenvana@example.com",
                    FirstName = "Nguyễn",
                    LastName = "Văn A"
                },
                new AppUser
                {
                    Id = "user456",
                    UserName = "tranthib@example.com",
                    Email = "tranthib@example.com",
                    FirstName = "Trần",
                    LastName = "Thị B"
                }
            };
            _context.AppUsers.AddRange(users);

            // Add test chats
            var chats = new List<UserChat>
            {
                new UserChat
                {
                    Id = 1,
                    IdUser = "user123",
                    ContentChat = "Xin chào mọi người!",
                    TimeChat = DateTime.Now.AddMinutes(-30)
                },
                new UserChat
                {
                    Id = 2,
                    IdUser = "user456",
                    ContentChat = "Chào bạn, bạn cần hỗ trợ gì không?",
                    TimeChat = DateTime.Now.AddMinutes(-25)
                },
                new UserChat
                {
                    Id = 3,
                    IdUser = "user123",
                    ContentChat = "Tôi muốn hỏi về sản phẩm mới",
                    TimeChat = DateTime.Now.AddMinutes(-20)
                }
            };
            _context.UserChats.AddRange(chats);

            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region GetChat Tests

        [Fact]
        public async Task TC_CHAT_01_GetChat_ReturnsAllChats()
        {
            // Act
            var result = await _controller.GetChat();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<ChatUserName>>>(result);
            var chatList = Assert.IsAssignableFrom<List<ChatUserName>>(actionResult.Value);

            // Kiểm tra đúng số lượng chat messages
            Assert.Equal(3, chatList.Count);

            // Kiểm tra chi tiết chat message đầu tiên
            var firstChat = chatList.OrderBy(c => c.TimeChat).First();
            Assert.Equal("user123", firstChat.IdUser);
            Assert.Equal("Xin chào mọi người!", firstChat.ContentChat);
            Assert.Equal("Nguyễn Văn A", firstChat.Name);

            // Kiểm tra DB
            var dbChatCount = await _context.UserChats.CountAsync();
            Assert.Equal(3, dbChatCount);
        }

        [Fact]
        public async Task TC_CHAT_02_GetChat_ReturnsEmptyList_WhenNoChatsExist()
        {
            // Arrange
            _context.UserChats.RemoveRange(_context.UserChats);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetChat();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<ChatUserName>>>(result);
            var chatList = Assert.IsAssignableFrom<List<ChatUserName>>(actionResult.Value);

            Assert.Empty(chatList);

            // Kiểm tra DB
            var dbChatCount = await _context.UserChats.CountAsync();
            Assert.Equal(0, dbChatCount);
        }

        #endregion

        #region AddChat Tests

        [Fact]
        public async Task TC_CHAT_03_AddChat_AddsNewChatMessage()
        {
            // Arrange
            var uploadChat = new UploadChat
            {
                IdUser = "user123",
                Content = "Tin nhắn mới từ unit test"
            };

            // Initial count
            var initialChatCount = await _context.UserChats.CountAsync();

            // Act
            var result = await _controller.AddChat(uploadChat);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Kiểm tra DB
            var currentChatCount = await _context.UserChats.CountAsync();
            Assert.Equal(initialChatCount + 1, currentChatCount);

            // Kiểm tra chat mới được tạo
            var newChat = await _context.UserChats.OrderByDescending(c => c.Id).FirstOrDefaultAsync();
            Assert.NotNull(newChat);
            Assert.Equal(uploadChat.IdUser, newChat.IdUser);
            Assert.Equal(uploadChat.Content, newChat.ContentChat);
            Assert.NotNull(newChat.TimeChat);

            // Kiểm tra SignalR broadcast được gọi
            _mockClients.Verify(clients => clients.All, Times.Once);
        }

        [Fact]
        public async Task TC_CHAT_04_AddChat_HandlesException()
        {
            // Arrange
            var uploadChat = new UploadChat
            {
                IdUser = "user123",
                Content = "Tin nhắn gây lỗi"
            };

            // Setup mock to throw exception on SaveChangesAsync
            var mockContext = new Mock<DPContext>(_options);
            mockContext.Setup(x => x.SaveChangesAsync(default))
                .ThrowsAsync(new Exception("Test exception"));

            // Setup controller with mocked context
            var controllerWithMockedContext = new UserChatsController(mockContext.Object, _mockHubContext.Object);

            // Act
            var result = await controllerWithMockedContext.AddChat(uploadChat);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Không cần kiểm tra DB vì đã mock SaveChangesAsync để ném ngoại lệ
        }

        [Fact]
        public async Task TC_CHAT_05_AddChat_WithNonExistentUser()
        {
            // Arrange
            var uploadChat = new UploadChat
            {
                IdUser = "nonexistentuser", // User không tồn tại
                Content = "Tin nhắn từ user không tồn tại"
            };

            // Initial count
            var initialChatCount = await _context.UserChats.CountAsync();

            // Act
            var result = await _controller.AddChat(uploadChat);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Kiểm tra DB - chat vẫn được thêm vào mặc dù user không tồn tại
            var currentChatCount = await _context.UserChats.CountAsync();
            Assert.Equal(initialChatCount + 1, currentChatCount);

            var newChat = await _context.UserChats.OrderByDescending(c => c.Id).FirstOrDefaultAsync();
            Assert.NotNull(newChat);
            Assert.Equal(uploadChat.IdUser, newChat.IdUser);
            Assert.Equal(uploadChat.Content, newChat.ContentChat);
        }

        #endregion
    }
}