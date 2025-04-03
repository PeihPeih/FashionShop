using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Controllers;
using API.Data;
using API.Models;
using API.Helper.SignalR;
using Xunit;
using Microsoft.AspNetCore.Http;
using API.Dtos;

namespace API.Tests
{
    public class MaGiamGiasControllerTests
    {
        private readonly DPContext _context;
        private readonly Mock<IHubContext<BroadcastHub, IHubClient>> _hubContextMock;
        private readonly MaGiamGiasController _controller;

        public MaGiamGiasControllerTests()
        {
            // Thiết lập InMemory Database để test không ảnh hưởng đến database thật
            var options = new DbContextOptionsBuilder<DPContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new DPContext(options);

            // Mock IHubContext để test SignalR mà không cần kết nối thực
            _hubContextMock = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var clientsMock = new Mock<IHubClients<IHubClient>>();
            var clientMock = new Mock<IHubClient>();

            clientsMock.Setup(clients => clients.All).Returns(clientMock.Object);
            _hubContextMock.Setup(hub => hub.Clients).Returns(clientsMock.Object);
            clientMock.Setup(client => client.BroadcastMessage()).Returns(Task.CompletedTask);

            // Khởi tạo controller
            _controller = new MaGiamGiasController(_context, _hubContextMock.Object);

            // Thêm dữ liệu giả lập ban đầu
            SeedData();
        }

        private void SeedData()
        {
            // Thêm một số mã giảm giá vào database giả lập
            var maGiamGia1 = new MaGiamGia { Id = 1, Code = "ABC12", SoTienGiam = 10000 };
            var maGiamGia2 = new MaGiamGia { Id = 2, Code = "XYZ34", SoTienGiam = 20000 };

            _context.MaGiamGias.AddRange(maGiamGia1, maGiamGia2);
            _context.SaveChanges();
        }

        /// <summary>
        /// Test Case MGG01: Kiểm tra phương thức GetMaGiamGias trả về danh sách khi có dữ liệu
        /// </summary>
        [Fact]
        public async Task GetMaGiamGias_ReturnsList_WhenDataExists()
        {
            // Act: Gọi API để lấy danh sách mã giảm giá
            var result = await _controller.GetMaGiamGias();

            // Assert: Kiểm tra kết quả trả về có phải danh sách không
            var okResult = Assert.IsType<ActionResult<IEnumerable<MaGiamGia>>>(result);
            var maGiamGias = Assert.IsAssignableFrom<List<MaGiamGia>>(okResult.Value);

            // Kiểm tra danh sách có đúng số lượng phần tử không
            Assert.Equal(2, maGiamGias.Count);
            Assert.Contains(maGiamGias, m => m.Code == "ABC12");
            Assert.Contains(maGiamGias, m => m.Code == "XYZ34");
        }

        /// <summary>
        /// Test Case MGG02: Kiểm tra phương thức GetMaGiamGias trả về danh sách rỗng khi không có dữ liệu
        /// </summary>
        [Fact]
        public async Task GetMaGiamGias_ReturnsEmptyList_WhenNoData()
        {
            // Arrange: Xóa toàn bộ dữ liệu trong bảng
            _context.MaGiamGias.RemoveRange(_context.MaGiamGias);
            _context.SaveChanges();

            // Act: Gọi API để lấy danh sách mã giảm giá
            var result = await _controller.GetMaGiamGias();

            // Assert: Kiểm tra danh sách trả về rỗng
            var okResult = Assert.IsType<ActionResult<IEnumerable<MaGiamGia>>>(result);
            var maGiamGias = Assert.IsAssignableFrom<List<MaGiamGia>>(okResult.Value);
            Assert.Empty(maGiamGias);
        }

        /// <summary>
        /// Test Case MGG03: Kiểm tra phương thức tạo mã giảm giá thành công
        /// </summary>
        [Fact]
        public async Task TaoMaGiamGia_ReturnsOk_WhenDataIsValid()
        {
            // Arrange: Tạo đối tượng đầu vào
            var uploadMaGiamGia = new UploadMaGiamGia { SoTienGiam = 15000 };

            // Act: Gọi API để tạo mã giảm giá mới
            var result = await _controller.TaoMaGiamGia(uploadMaGiamGia);

            // Assert: Kiểm tra kết quả trả về là OkResult
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

            // Kiểm tra mã giảm giá mới có tồn tại trong database không
            var createdMaGiamGia = _context.MaGiamGias.Last();
            Assert.Equal(15000, createdMaGiamGia.SoTienGiam);
            Assert.Equal(5, createdMaGiamGia.Code.Length); // Kiểm tra độ dài mã giảm giá
            _hubContextMock.Verify(h => h.Clients.All.BroadcastMessage(), Times.Once()); // Kiểm tra SignalR được gọi
        }

        /// <summary>
        /// Test Case MGG06: Kiểm tra xóa mã giảm giá thành công khi ID hợp lệ
        /// </summary>
        [Fact]
        public async Task DeleteMaGiamGias_ReturnsOk_WhenIdIsValid()
        {
            // Act: Gọi API xóa mã giảm giá có ID = 1
            var result = await _controller.DeleteMaGiamGias(1);

            // Assert: Kiểm tra kết quả trả về là OkResult
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

            // Kiểm tra mã giảm giá đã bị xóa khỏi database
            var deletedMaGiamGia = await _context.MaGiamGias.FindAsync(1);
            Assert.Null(deletedMaGiamGia);

            // Kiểm tra SignalR có được gọi không
            _hubContextMock.Verify(h => h.Clients.All.BroadcastMessage(), Times.Once());
        }

        /// <summary>
        /// Test Case MGG07: Kiểm tra phương thức trả về NotFound khi ID không tồn tại
        /// </summary>
        [Fact]
        public async Task DeleteMaGiamGias_ReturnsNotFound_WhenIdIsInvalid()
        {
            // Act: Gọi API xóa mã giảm giá với ID không tồn tại
            var result = await _controller.DeleteMaGiamGias(999);

            // Assert: Kiểm tra kết quả trả về là NotFoundObjectResult
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var error = notFoundResult.Value;

            // Kiểm tra thông báo lỗi
            var messageProperty = error.GetType().GetProperty("message");
            var messageValue = messageProperty.GetValue(error) as string;
            Assert.Equal("Không tìm thấy mã giảm giá với ID 999", messageValue);

            // Kiểm tra không có mã giảm giá nào bị xóa
            Assert.Equal(2, _context.MaGiamGias.Count());

            // Kiểm tra không gọi SignalR
            _hubContextMock.Verify(h => h.Clients.All.BroadcastMessage(), Times.Never());
        }
    }
}
