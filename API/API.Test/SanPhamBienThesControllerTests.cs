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
    public class SanPhamBienThesControllerTests : TestBase {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly SanPhamBienThesController _controller;

        public SanPhamBienThesControllerTests() : base() {
            _context = new DPContext(_options);

            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            _controller = new SanPhamBienThesController(_context, _hubContext);
        }

        // Spbt01: Kiểm tra trả về danh sách biến thể sản phẩm gồm thông tin màu sắc, sản phẩm và kích thước.
        [Fact]
        public async Task GetSanPhamBienThes_ReturnsListOfGiaSanPhamMauSacSanPhamSize() {
            // Arrange: tạo dữ liệu liên quan
            var mau = new MauSac { MaMau = "#123456", Id_Loai = 1 };
            var sp = new SanPham { Ten = "Sản phẩm A" };
            var size = new Size { TenSize = "M", Id_Loai = 1 };

            _context.MauSacs.Add(mau);
            _context.SanPhams.Add(sp);
            _context.Sizes.Add(size);
            await _context.SaveChangesAsync();

            var bienThe = new SanPhamBienThe {
                Id_Mau = mau.Id,
                Id_SanPham = sp.Id,
                SizeId = size.Id,
                SoLuongTon = 100
            };

            _context.SanPhamBienThes.Add(bienThe);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetSanPhamBienThes();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<GiaSanPhamMauSacSanPhamSize>>>(result);
            var okResult = Assert.IsAssignableFrom<IEnumerable<GiaSanPhamMauSacSanPhamSize>>(actionResult.Value);

            Assert.NotEmpty(okResult);
        }

        // Spbt02: Kiểm tra trả về NotFound khi yêu cầu một sản phẩm không tồn tại.
        [Fact]
        public async Task Get_ReturnsNotFound_WhenItemDoesNotExist() {
            // Arrange
            var nonExistentId = 99999; // ID này đảm bảo không có trong DB

            // Act
            var result = await _controller.Get(nonExistentId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }
        
        // Spbt03: Kiểm tra trả về đúng thông tin biến thể sản phẩm khi biến thể tồn tại trong cơ sở dữ liệu.
        [Fact]
        public async Task Get_ReturnsItem_WhenItemExists() {
            // Arrange
            var item = new SanPhamBienThe {
                Id_Mau = 1,
                Id_SanPham = 1,
                SizeId = 1,
                SoLuongTon = 100
            };

            // Seed dữ liệu vào DB
            _context.SanPhamBienThes.Add(item);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Get(item.Id);

            // Assert
            var actionResult = Assert.IsType<ActionResult<SanPhamBienThe>>(result);
            var returnValue = Assert.IsType<SanPhamBienThe>(actionResult.Value);

            Assert.Equal(item.Id, returnValue.Id);
            Assert.Equal(item.SoLuongTon, returnValue.SoLuongTon);
        }


        // Spbt04: Kiểm tra cập nhật biến thể sản phẩm thành công và trả về Ok.
        [Fact]
        public async Task PutSanPhamBienThe_ReturnsOk_WhenUpdateIsSuccessful() {
            // Arrange
            var entity = new SanPhamBienThe {
                Id_Mau = 1,
                Id_SanPham = 1,
                SizeId = 1,
                SoLuongTon = 10
            };
            _context.SanPhamBienThes.Add(entity);
            await _context.SaveChangesAsync();

            var upload = new UploadSanPhamBienThe {
                MauId = 2,
                SanPhamId = 2,
                SizeId = 2,
                SoLuongTon = 20
            };

            // Act
            var result = await _controller.PutSanPhamBienThe(entity.Id, upload);

            // Assert
            Assert.IsType<OkResult>(result);

            var updated = await _context.SanPhamBienThes.FindAsync(entity.Id);
            Assert.Equal(2, updated.Id_Mau);
            Assert.Equal(2, updated.Id_SanPham);
            Assert.Equal(2, updated.SizeId);
            Assert.Equal(20, updated.SoLuongTon);
        }

        // Spbt05: Kiểm tra trả về BadRequest khi ModelState không hợp lệ do giá trị SoLuongTon sai định dạng hoặc thiếu dữ liệu.
        [Fact]
        public async Task PostSanPhamBienThe_ReturnsBadRequest_WhenModelStateInvalid() {
            // Arrange
            _controller.ModelState.AddModelError("SoLuongTon", "Required");

            var upload = new UploadSanPhamBienThe {
                SanPhamId = 1,
                SizeId = 1,
                MauId = 1,
                SoLuongTon = -1 // thiếu hợp lệ hoặc lỗi dữ liệu
            };

            // Act
            var result = await _controller.PostSanPhamBienThe(upload);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("SoLuongTon", badRequest.Value.ToString());
            Assert.Contains(_context.Notifications, n => n.TranType == "Add");

        }

        // Spbt06: Kiểm tra xóa biến thể sản phẩm thành công khi biến thể tồn tại trong cơ sở dữ liệu.
        [Fact]
        public async Task DeleteSanPhamBienTh_ReturnsOk_WhenSanPhamBienTheExists() {
            // Arrange: Tạo dữ liệu thật
            var spbt = new SanPhamBienThe {
                Id_SanPham = 1,
                Id_Mau = 1,
                SizeId = 1,
                SoLuongTon = 10
            };
            _context.SanPhamBienThes.Add(spbt);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.DeleteSanPhamBienTh(spbt.Id);

            // Assert
            Assert.IsType<OkResult>(result);
            Assert.DoesNotContain(_context.SanPhamBienThes, s => s.Id == spbt.Id);
        }
    }
}
