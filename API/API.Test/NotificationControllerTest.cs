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
using System.Threading.Tasks;
using Xunit;

namespace API.Test
{
    public class NotificationsControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly NotificationsController _controller;

        public NotificationsControllerTests() : base()
        {
            _context = new DPContext(_options);

            // Setup mock for SignalR hub
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            _controller = new NotificationsController(_context, _hubContext);
        }

        // Helper method to create test notifications
        private async Task<Notification> CreateTestNotificationAsync(string tenSanPham = "Test Product", string tranType = "Add")
        {
            var notification = new Notification
            {
                TenSanPham = tenSanPham,
                TranType = tranType
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            return notification;
        }

        // ---------------------- GET NOTIFICATION COUNT --------------------------
        // NTF01: Get notification count - Should return correct count of notifications
        [Fact]
        public async Task GetNotificationCount_ReturnsCorrectCount()
        {
            // Arrange
            await CreateTestNotificationAsync("Product 1", "Add");
            await CreateTestNotificationAsync("Product 2", "Edit");
            await CreateTestNotificationAsync("Product 3", "Delete");

            // Act
            var result = await _controller.GetNotificationCount();

            // Assert
            var actionResult = Assert.IsType<ActionResult<NotificationCountResult>>(result);
            var countResult = Assert.IsType<NotificationCountResult>(actionResult.Value);

            Assert.Equal(3, countResult.Count);
        }

        // NTF02: Get notification count - Should return zero when no notifications exist
        [Fact]
        public async Task GetNotificationCount_ReturnsZero_WhenNoNotificationsExist()
        {
            // Arrange - ensure no notifications in database
            foreach (var notification in _context.Notifications)
            {
                _context.Notifications.Remove(notification);
            }
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetNotificationCount();

            // Assert
            var actionResult = Assert.IsType<ActionResult<NotificationCountResult>>(result);
            var countResult = Assert.IsType<NotificationCountResult>(actionResult.Value);

            Assert.Equal(0, countResult.Count);
        }

        // ---------------------- GET NOTIFICATION MESSAGE --------------------------
        // NTF03: Get notification message - Should return all notifications in descending order
        [Fact]
        public async Task GetNotificationMessage_ReturnsAllNotificationsInDescendingOrder()
        {
            // Arrange
            await CreateTestNotificationAsync("Product 1", "Add");
            await CreateTestNotificationAsync("Product 2", "Edit");
            await CreateTestNotificationAsync("Product 3", "Delete");

            // Act
            var result = await _controller.GetNotificationMessage();

            // Assert
            var actionResult = Assert.IsType<ActionResult<List<NotificationResult>>>(result);
            var notifications = Assert.IsType<List<NotificationResult>>(actionResult.Value);

            Assert.Equal(3, notifications.Count);

            // Check if they are in descending order (latest first)
            // Since we've just created them in sequence, the latest one should be "Product 3"
            Assert.Equal("Product 3", notifications[0].TenSanPham);
            Assert.Equal("Product 2", notifications[1].TenSanPham);
            Assert.Equal("Product 1", notifications[2].TenSanPham);
        }

        // NTF04: Get notification message - Should return empty list when no notifications exist
        [Fact]
        public async Task GetNotificationMessage_ReturnsEmptyList_WhenNoNotificationsExist()
        {
            // Arrange - ensure no notifications in database
            foreach (var notification in _context.Notifications)
            {
                _context.Notifications.Remove(notification);
            }
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetNotificationMessage();

            // Assert
            var actionResult = Assert.IsType<ActionResult<List<NotificationResult>>>(result);
            var notifications = Assert.IsType<List<NotificationResult>>(actionResult.Value);

            Assert.Empty(notifications);
        }

        // NTF05: Get notification message - Should return correct notification properties
        [Fact]
        public async Task GetNotificationMessage_ReturnsCorrectNotificationProperties()
        {
            // Arrange
            await CreateTestNotificationAsync("Test Product", "Add");

            // Act
            var result = await _controller.GetNotificationMessage();

            // Assert
            var actionResult = Assert.IsType<ActionResult<List<NotificationResult>>>(result);
            var notifications = Assert.IsType<List<NotificationResult>>(actionResult.Value);

            Assert.Single(notifications);
            var notification = notifications.First();

            Assert.Equal("Test Product", notification.TenSanPham);
            Assert.Equal("Add", notification.TranType);
        }

        // ---------------------- DELETE NOTIFICATIONS --------------------------
        // NTF06: Delete notifications - Should delete all notifications
        [Fact]
        public async Task DeleteNotifications_DeletesAllNotifications()
        {
            // Arrange
            await CreateTestNotificationAsync("Product 1", "Add");
            await CreateTestNotificationAsync("Product 2", "Edit");
            await CreateTestNotificationAsync("Product 3", "Delete");

            // Act
            var result = await _controller.DeleteNotifications();

            // Assert
            Assert.IsType<NoContentResult>(result);

            // Verify all notifications are deleted
            var remainingNotifications = await _context.Notifications.ToListAsync();
            Assert.Empty(remainingNotifications);
        }

        // NTF07: Delete notifications - Should not throw exception when no notifications exist
        [Fact]
        public async Task DeleteNotifications_DoesNotThrowException_WhenNoNotificationsExist()
        {
            // Arrange - ensure no notifications in database
            foreach (var notification in _context.Notifications)
            {
                _context.Notifications.Remove(notification);
            }
            await _context.SaveChangesAsync();

            // Act & Assert - Should not throw exception
            var result = await _controller.DeleteNotifications();
            Assert.IsType<NoContentResult>(result);
        }

        // NTF08: Delete notifications - Should broadcast message after deletion
        [Fact]
        public async Task DeleteNotifications_BroadcastsMessage()
        {
            // Arrange
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();

            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);

            var controller = new NotificationsController(_context, mockHub.Object);

            // Act
            var result = await controller.DeleteNotifications();

            // Assert
            mockClientProxy.Verify(x => x.BroadcastMessage(), Times.Once);
        }
    }
}