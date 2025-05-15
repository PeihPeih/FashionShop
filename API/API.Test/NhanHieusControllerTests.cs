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

namespace API.Test
{
    public class NhanHieusControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly NhanHieusController _controller;

        public NhanHieusControllerTests() : base()
        {
            // Khởi tạo InMemory Database để test
            _context = new DPContext(_options);

            // Mock IHubContext để giả lập BroadcastHub
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            // Khởi tạo controller
            _controller = new NhanHieusController(_context, _hubContext);
        }

        // ---------------------- GET ALL ------------------------
        // NhanHieu01: Lấy danh sách nhãn hiệu khi có dữ liệu
        [Fact]
        public async Task NhanHieu01_GetThuongHieus_ReturnsList_WhenDataExists()
        {
            // Arrange - Chuẩn bị dữ liệu
            var nhanHieu1 = new NhanHieu
            {
                Ten = "Nhãn hiệu A",
                DateCreate = DateTime.Now
            };
            var nhanHieu2 = new NhanHieu
            {
                Ten = "Nhãn hiệu B",
                DateCreate = DateTime.Now
            };
            _context.NhanHieus.AddRange(nhanHieu1, nhanHieu2);
            await _context.SaveChangesAsync();

            // Act - Gọi API
            var result = await _controller.GetThuongHieus();

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NhanHieu>>>(result);
            var data = Assert.IsAssignableFrom<IEnumerable<NhanHieu>>(actionResult.Value);
            Assert.NotEmpty(data); // Kiểm tra danh sách không rỗng
            Assert.Contains(data, n => n.Ten == "Nhãn hiệu A");
            Assert.Contains(data, n => n.Ten == "Nhãn hiệu B");
        }

        // NhanHieu02: Lấy nhãn hiệu theo Id khi Id tồn tại
        [Fact]
        public async Task NhanHieu02_GetThuongHieu_ReturnsNhanHieu_WhenIdExists()
        {
            // Arrange - Chuẩn bị dữ liệu
            var nhanHieu = new NhanHieu
            {
                Ten = "Nhãn hiệu C",
                DateCreate = DateTime.Now
            };
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            // Act - Gọi API với Id = 1
            var result = await _controller.GetThuongHieu(nhanHieu.Id);

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<NhanHieu>>(result);
            var data = Assert.IsType<NhanHieu>(actionResult.Value);
            Assert.Equal("Nhãn hiệu C", data.Ten);
            Assert.Equal(nhanHieu.DateCreate, data.DateCreate);
        }

        // NhanHieu03: Lấy nhãn hiệu theo Id khi Id không tồn tại
        [Fact]
        public async Task NhanHieu03_GetThuongHieu_ReturnsNotFound_WhenIdDoesNotExist()
        {
            // Act - Gọi API với Id không tồn tại
            var result = await _controller.GetThuongHieu(999);

            // Assert - Kiểm tra kết quả
            var notFoundResult = Assert.IsType<NotFoundResult>(result.Result); // Kiểm tra API trả về thông báo không tìm thấy
            Assert.Equal("Nhãn hiệu bạn lấy không tồn tại", notFoundResult.ToString());
        }
        // ---------------------- POST ------------------------
        // NhanHieu04: Thêm nhãn hiệu với dữ liệu hợp lệ
        [Fact]
        public async Task NhanHieu04_PostNhanHieu_Success_WhenDataIsValid()
        {
            // Arrange - Chuẩn bị dữ liệu
            var upload = new UploadBrand
            {
                Name = "Nhãn hiệu D"
            };

            // Act - Gọi API để thêm nhãn hiệu
            var result = await _controller.PostNhanHieu(upload);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<ActionResult<NhanHieu>>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var nhanHieu = await _context.NhanHieus.FirstOrDefaultAsync(n => n.Ten == "Nhãn hiệu D");
            Assert.NotNull(nhanHieu); // Kiểm tra nhãn hiệu đã được thêm
            Assert.Equal("Nhãn hiệu D", nhanHieu.Ten);
            Assert.NotNull(nhanHieu.DateCreate); // Kiểm tra ngày tạo không null

            // Kiểm tra thông báo
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "Nhãn hiệu D");
            Assert.NotNull(notification);
            Assert.Equal("Add", notification.TranType);
        }

        // NhanHieu05: Cập nhật nhãn hiệu khi Id tồn tại
        [Fact]
        public async Task NhanHieu05_PutNhanHieu_Success_WhenIdExists()
        {
            // Arrange - Chuẩn bị dữ liệu
            var nhanHieu = new NhanHieu
            {
                Ten = "Nhãn hiệu E",
                DateCreate = DateTime.Now.AddDays(-1)
            };
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            var upload = new UploadBrand
            {
                Name = "Nhãn hiệu E Updated"
            };

            // Act - Gọi API để cập nhật
            var result = await _controller.PutNhanHieu(nhanHieu.Id, upload);

            // Assert - Kiểm tra kết quả
            var noContentResult = Assert.IsType<NoContentResult>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var updatedNhanHieu = await _context.NhanHieus.FindAsync(nhanHieu.Id);
            Assert.Equal("Nhãn hiệu E Updated", updatedNhanHieu.Ten);
            Assert.True(updatedNhanHieu.DateCreate > nhanHieu.DateCreate); // Kiểm tra ngày cập nhật mới hơn

            // Kiểm tra thông báo
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "Nhãn hiệu E Updated");
            Assert.NotNull(notification);
            Assert.Equal("Edit", notification.TranType);
        }

        // NhanHieu06: Cập nhật nhãn hiệu khi Id không tồn tại
        [Fact]
        public async Task NhanHieu06_PutNhanHieu_ReturnsNotFound_WhenIdDoesNotExist()
        {

            // Arrange - Chuẩn bị dữ liệu
            var upload = new UploadBrand
            {
                Name = "Nhãn hiệu F"
            };

            // Act - Gọi API với Id không tồn tại
            var result = await _controller.PutNhanHieu(999, upload);

            // Assert - Kiểm tra kết quả
            var notFoundResult = Assert.IsType<NotFoundResult>(result); // Kiểm tra API trả về thông báo không tìm thấy
            Assert.Equal("Nhãn hiệu bạn sửa không tồn tại", notFoundResult.ToString());
        }
        // NhanHieu07: Cập nhật nhãn hiệu khi kết nối DB không thành công 
        [Fact]
        public async Task NhanHieu07_PutNhanHieu_ReturnsInternalServerError_WhenExceptionOccurs()
        {

            // Arrange - Tạo DPContext với connection string sai để gây lỗi
            var badOptions = new DbContextOptionsBuilder<DPContext>()
                .UseSqlServer("Server=INVALID_SERVER;Database=EFashionShop;Integrated Security=True")
                .Options;

            var contextWithError = new DPContext(badOptions);
            var controllerWithError = new NhanHieusController(contextWithError, _hubContext);

            var nhanHieu = new NhanHieu
            {
                Ten = "Nhãn hiệu E",
                DateCreate = DateTime.Now.AddDays(-1)
            };
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            var upload = new UploadBrand
            {
                Name = "Nhãn hiệu E Updated"
            };

            // Act - Gọi API
            var result = await controllerWithError.PutNhanHieu(nhanHieu.Id, upload);

            // Assert - Kiểm tra kết quả
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode); // Kiểm tra mã lỗi 500
            var error = statusCodeResult.Value;
            var messageProperty = error.GetType().GetProperty("message");
            var messageValue = messageProperty.GetValue(error) as string;
            Assert.Equal("Đã xảy ra lỗi trong quá trình khi sửa nhãn hiệu", messageValue); // Kiểm tra thông báo lỗi
        }

        // NhanHieu08: Xóa nhãn hiệu khi Id tồn tại và không có sản phẩm liên quan
        [Fact]
        public async Task NhanHieu08_DeleteThuongHieu_Success_WhenIdExistsAndNoRelatedProducts()
        {

            // Arrange - Chuẩn bị dữ liệu
            var nhanHieu = new NhanHieu
            {
                Ten = "Nhãn hiệu H",
                DateCreate = DateTime.Now
            };
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            // Act - Gọi API để xóa
            var result = await _controller.DeleteThuongHieu(nhanHieu.Id);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<OkResult>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var deletedNhanHieu = await _context.NhanHieus.FindAsync(nhanHieu.Id);
            Assert.Null(deletedNhanHieu); // Kiểm tra nhãn hiệu đã bị xóa

            // Kiểm tra thông báo
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "Nhãn hiệu H");
            Assert.NotNull(notification);
            Assert.Equal("Delete", notification.TranType);
        }
    }
}

