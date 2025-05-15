using API.Controllers;
using API.Data;
using API.Helper.SignalR;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace API.Test
{
    public class NotificationsControllerTest : TestBase
    {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly NotificationsController _controller;
        public NotificationsControllerTest() : base()
        {
            // Khởi tạo InMemoryDatabase
            _context = new DPContext(_options);

            // Giả lập SignalR HubContext
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            // Khởi tạo controller
            _controller = new NotificationsController(_context, _hubContext);

            // Làm sạch DB trước khi chạy mỗi bài kiểm thử
            Cleanup();
        }

        // Phương thức làm sạch DB
        private void Cleanup()
        {
            _context.Notifications.RemoveRange(_context.Notifications);
            _context.SaveChanges();
        }

        // NOTF01: Kiểm tra lấy số lượng thông báo trả về đúng khi có dữ liệu trong DB
        [Fact]
        public async Task GetNotificationCount_ShouldReturnCorrectCount_WhenDataExists()
        {
            // Arrange: Làm sạch DB và thêm 2 thông báo
            Cleanup();
            var notification1 = new Notification { TenSanPham = "Product1", TranType = "Type1" };
            var notification2 = new Notification { TenSanPham = "Product2", TranType = "Type2" };
            _context.Notifications.AddRange(notification1, notification2);
            await _context.SaveChangesAsync();

            // Kiểm tra DB trước khi gọi API
            Assert.Equal(2, await _context.Notifications.CountAsync());

            // Act: Gọi API lấy số lượng thông báo
            var result = await _controller.GetNotificationCount();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<NotificationCountResult>>(result);
            var countResult = Assert.IsType<NotificationCountResult>(actionResult.Value);
            Assert.Equal(2, countResult.Count);

            // Kiểm tra trực tiếp DB sau khi gọi API
            Assert.Equal(2, await _context.Notifications.CountAsync());
        }

        // NOTF02: Kiểm tra lấy số lượng thông báo trả về 0 khi không có dữ liệu
        [Fact]
        public async Task GetNotificationCount_ShouldReturnZero_WhenNoData()
        {
            // Arrange: Làm sạch DB
            Cleanup();
            Assert.Equal(0, await _context.Notifications.CountAsync());

            // Act: Gọi API lấy số lượng thông báo
            var result = await _controller.GetNotificationCount();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<NotificationCountResult>>(result);
            var countResult = Assert.IsType<NotificationCountResult>(actionResult.Value);
            Assert.Equal(0, countResult.Count);

            // Kiểm tra trực tiếp DB sau khi gọi API
            Assert.Equal(0, await _context.Notifications.CountAsync());
        }

        // NOTF03: Kiểm tra lấy danh sách thông báo trả về đúng khi có dữ liệu
        [Fact]
        public async Task GetNotificationMessage_ShouldReturnAll_WhenDataExists()
        {
            // Arrange: Làm sạch DB và thêm 2 thông báo
            Cleanup();
            var notification1 = new Notification { TenSanPham = "Product1", TranType = "Type1" };
            var notification2 = new Notification { TenSanPham = "Product2", TranType = "Type2" };
            _context.Notifications.AddRange(notification1, notification2);
            await _context.SaveChangesAsync();

            // Kiểm tra DB trước khi gọi API
            Assert.Equal(2, await _context.Notifications.CountAsync());

            // Act: Gọi API lấy danh sách thông báo
            var result = await _controller.GetNotificationMessage();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<List<NotificationResult>>>(result);
            var list = Assert.IsType<List<NotificationResult>>(actionResult.Value);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.TenSanPham == "Product1" && x.TranType == "Type1");
            Assert.Contains(list, x => x.TenSanPham == "Product2" && x.TranType == "Type2");

            // Kiểm tra thứ tự giảm dần theo Id bằng cách truy vấn DB
            var dbNotifications = await _context.Notifications
                .OrderByDescending(x => x.Id)
                .Select(x => new { x.TenSanPham, x.TranType })
                .ToListAsync();
            for (int i = 0; i < list.Count; i++)
            {
                Assert.Equal(dbNotifications[i].TenSanPham, list[i].TenSanPham);
                Assert.Equal(dbNotifications[i].TranType, list[i].TranType);
            }

            // Kiểm tra trực tiếp DB sau khi gọi API
            Assert.Equal(2, await _context.Notifications.CountAsync());
        }

        // NOTF04: Kiểm tra lấy danh sách thông báo trả về rỗng khi không có dữ liệu
        [Fact]
        public async Task GetNotificationMessage_ShouldReturnEmpty_WhenNoData()
        {
            // Arrange: Làm sạch DB
            Cleanup();
            Assert.Equal(0, await _context.Notifications.CountAsync());

            // Act: Gọi API lấy danh sách thông báo
            var result = await _controller.GetNotificationMessage();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<List<NotificationResult>>>(result);
            var list = Assert.IsType<List<NotificationResult>>(actionResult.Value);
            Assert.Empty(list);

            // Kiểm tra trực tiếp DB sau khi gọi API
            Assert.Equal(0, await _context.Notifications.CountAsync());
        }

        // NOTF05: Kiểm tra xóa tất cả thông báo thành công khi có dữ liệu
        [Fact]
        public async Task DeleteNotifications_ShouldClearAll_WhenDataExists()
        {
            // Arrange: Làm sạch DB và thêm 2 thông báo
            Cleanup();
            var notification1 = new Notification { TenSanPham = "Product1", TranType = "Type1" };
            var notification2 = new Notification { TenSanPham = "Product2", TranType = "Type2" };
            _context.Notifications.AddRange(notification1, notification2);
            await _context.SaveChangesAsync();

            // Kiểm tra DB trước khi xóa
            Assert.Equal(2, await _context.Notifications.CountAsync());

            // Giả lập SignalR
            var mockClientProxy = Mock.Get(_hubContext.Clients.All);

            // Act: Gọi API xóa tất cả thông báo
            var result = await _controller.DeleteNotifications();

            // Assert: Kiểm tra kết quả
            var noContent = Assert.IsType<NoContentResult>(result);
            Assert.Equal(204, noContent.StatusCode);

            // Kiểm tra trực tiếp DB: phải rỗng
            Assert.Equal(0, await _context.Notifications.CountAsync());

            // Kiểm tra SignalR được gọi
            mockClientProxy.Verify(c => c.BroadcastMessage(), Times.Once());
        }

        // NOTF06: Kiểm tra xóa tất cả thông báo thành công khi DB rỗng
        [Fact]
        public async Task DeleteNotifications_ShouldReturnNoContent_WhenNoData()
        {
            // Arrange: Làm sạch DB
            Cleanup();
            Assert.Equal(0, await _context.Notifications.CountAsync());

            // Giả lập SignalR
            var mockClientProxy = Mock.Get(_hubContext.Clients.All);

            // Act: Gọi API xóa tất cả thông báo
            var result = await _controller.DeleteNotifications();

            // Assert: Kiểm tra kết quả
            var noContent = Assert.IsType<NoContentResult>(result);
            Assert.Equal(204, noContent.StatusCode);

            // Kiểm tra trực tiếp DB: vẫn rỗng
            Assert.Equal(0, await _context.Notifications.CountAsync());

            // Kiểm tra SignalR được gọi
            mockClientProxy.Verify(c => c.BroadcastMessage(), Times.Once());
        }
    }
}
