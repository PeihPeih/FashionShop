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
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace API.Test {
    public class LoaisControllerTests : TestBase {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly LoaisController _controller;

        public LoaisControllerTests() : base() {
            _context = new DPContext(_options);

            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            _controller = new LoaisController(_context, _hubContext);
        }

        // Loais01: Kiểm tra trả về danh sách tất cả loại khi có dữ liệu
        [Fact]
        public async Task GetLoais_ReturnsAllLoais() {
            // Arrange: thêm dữ liệu vào context
            var loai1 = new Loai { Ten = "TestLoai1" };
            var loai2 = new Loai { Ten = "TestLoai2" };
            _context.Loais.AddRange(loai1, loai2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetLoais();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<Loai>>>(result);
            var returnValue = Assert.IsAssignableFrom<IEnumerable<Loai>>(actionResult.Value);
            Assert.Contains(returnValue, l => l.Ten == "TestLoai1");
            Assert.Contains(returnValue, l => l.Ten == "TestLoai2");
        }

        // Loais02: Kiểm tra trả về danh sách sản phẩm theo loại khi loại và sản phẩm tồn tại
        [Fact]
        public async Task GetLoaiIdProducts_ReturnsList_WhenLoaiAndSanPhamExist() {
            // Arrange
            var loai = new Loai { Ten = "TestLoai" };
            _context.Loais.Add(loai);
            await _context.SaveChangesAsync();

            var sanPham = new SanPham { Ten = "TestSp", Id_Loai = loai.Id };
            _context.SanPhams.Add(sanPham);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetLoaiIdProducts(loai.Id);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPhamLoai>>>(result);
            var returnValue = Assert.IsAssignableFrom<IEnumerable<SanPhamLoai>>(actionResult.Value);
            Assert.Single(returnValue);
        }

        // Loais03: Kiểm tra trả về thông tin loại khi ID tồn tại
        [Fact]
        public async Task GetLoai_ReturnsLoai_WhenExists() {
            // Arrange
            var loai = new Loai { Ten = "TestLoai" };
            _context.Loais.Add(loai);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetLoai(loai.Id);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Loai>>(result);
            var returnValue = Assert.IsType<Loai>(actionResult.Value);
            Assert.Equal("TestLoai", returnValue.Ten);
        }

        // Loais04: Kiểm tra trả về NotFound khi loại không tồn tại
        [Fact]
        public async Task GetLoai_ReturnsNotFound_WhenLoaiDoesNotExist() {
            // Act
            var result = await _controller.GetLoai(9999);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Loai>>(result);
            Assert.IsType<NotFoundResult>(result.Result);
        }

        // Loais05: Kiểm tra cập nhật loại thành công 
        [Fact]
        public async Task PutLoai_ReturnsOk_WhenUpdateSuccess() {
            // Arrange
            var loai = new Loai { Ten = "TestLoai" };
            _context.Loais.Add(loai);
            await _context.SaveChangesAsync();

            var upload = new UploadCategory { Name = "Updated TestLoai" };

            // Act
            var result = await _controller.PutLoai(loai.Id, upload);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            var updatedLoai = await _context.Loais.FindAsync(loai.Id);
            Assert.Equal("Updated TestLoai", updatedLoai.Ten);

            var mockClientProxy = Mock.Get(_hubContext.Clients.All);
            mockClientProxy.Verify(c => c.BroadcastMessage(), Times.Once());
        }

        // Loais06: Kiểm tra trả về NotFound khi cập nhật loại không tồn tại
        [Fact]
        public async Task PutLoai_ReturnsNotFound_WhenLoaiNotExist() {
            // Arrange
            var upload = new UploadCategory { Name = "TestLoai" };

            // Act
            var result = await _controller.PutLoai(9999, upload);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(result);
        }

        // Loais07: Kiểm tra xử lý ngoại lệ DbUpdateConcurrencyException khi cập nhật loại bị xung đột dữ liệu
        [Fact]
        public async Task PutLoai_ShouldHandleDbUpdateConcurrencyException_WhenDbUpdateConcurrencyExceptionOccurs() {
            // Arrange
            var loai = new Loai { Id = 1, Ten = "Loai 1" };
            _context.Loais.Add(loai);
            await _context.SaveChangesAsync();

            var uploadCategory = new UploadCategory { Name = "Loai Updated" };

            // Giả lập dữ liệu đã bị thay đổi
            var mockDbContext = new Mock<DPContext>(_options);
            mockDbContext.Setup(db => db.Loais.FindAsync(It.IsAny<int>())).Returns(ValueTask.FromResult(loai));
            var controller = new LoaisController(mockDbContext.Object, _hubContext);

            // Act
            var result = await controller.PutLoai(loai.Id, uploadCategory);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(409, conflictResult.StatusCode);
            Assert.Equal("Có xung đột khi cập nhật dữ liệu, vui lòng thử lại sau.", conflictResult.Value);
        }

        // Loais08: Kiểm tra chức năng thêm loại mới, đảm bảo loại được thêm vào cơ sở dữ liệu
        [Fact]
        public async Task PostLoai_ValidInput_AddsNotificationAndLoai_ReturnsOk() {
            // Arrange
            var upload = new UploadCategory { Name = "TestCategory" };

            // Act
            var result = await _controller.PostLoai(upload);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Loai>>(result); // Kiểm tra kiểu ActionResult<Loai>
            var okResult = Assert.IsType<OkResult>(actionResult.Result); // Lấy OkResult từ thuộc tính Result
            Assert.Equal(200, okResult.StatusCode);

            var notification = _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "TestCategory" && n.TranType == "Add");
            Assert.NotNull(notification);

            var loai = _context.Loais.FirstOrDefault(n => n.Ten == upload.Name);
            Assert.NotNull(loai);
        }

        // Loais09: Kiểm tra việc xóa loại khi loại không tồn tại trong cơ sở dữ liệu, đảm bảo trả về kết quả NotFound.
        [Fact]
        public async Task DeleteLoai_ShouldReturnNotFound_WhenCategoryDoesNotExist() {
            // Arrange
            int nonExistentId = 999;

            // Act
            var result = await _controller.DeleteLoai(nonExistentId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // Loais10: Kiểm tra việc xóa một loại (Loai) cùng với các dữ liệu liên quan như sản phẩm, kích thước và màu sắc, đồng thời đảm
        // bảo trả về kết quả Ok 
        [Fact]
        public async Task DeleteLoai_WithProducts_ShouldDeleteLoaiAndRelatedData() {
            // Arrange
            var category = new Loai { Ten = "Test Category" };


            _context.Loais.Add(category);
            _context.SaveChanges();

            var loaiId = category.Id;
            var products = new List<SanPham> { new SanPham { Id_Loai = loaiId } };
            var sizes = new List<Size> { new Size { Id_Loai = loaiId } };
            var mausacs = new List<MauSac> { new MauSac { Id_Loai = loaiId } };

            _context.SanPhams.AddRange(products);
            _context.Sizes.AddRange(sizes);
            _context.MauSacs.AddRange(mausacs);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.DeleteLoai(loaiId);

            // Assert
            Assert.IsType<OkResult>(result); // Ensure Ok() is returned
            var notification = _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "Test Category" && n.TranType == "Delete");
            Assert.NotNull(notification);
        }
    }
}
