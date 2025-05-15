using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helper.SignalR;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace API.Test {
    public class MauSacsControllerTests : TestBase {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly MauSacsController _controller;

        public MauSacsControllerTests() : base() {
            _context = new DPContext(_options);

            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            _controller = new MauSacsController(_context, _hubContext);
        }

        // Mau01: Kiểm tra trả về dữ liệu kết hợp giữa MauSac và Loai khi có dữ liệu.
        [Fact]
        public async Task GetMauSacs_ReturnsJoinedData_WhenDataExists() {
            // Arrange
            var loai = new Loai { Ten = "Áo" };
            _context.Loais.Add(loai);
            await _context.SaveChangesAsync(); // Giúp EF generate Id

            var mauSac = new MauSac { Id_Loai = loai.Id, MaMau = "#FFFFFF" };
            _context.MauSacs.Add(mauSac);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetMauSacs();
            var okResult = Assert.IsType<ActionResult<IEnumerable<MauSacLoai>>>(result);
            var data = Assert.IsAssignableFrom<IEnumerable<MauSacLoai>>(okResult.Value);

            // Assert
            Assert.NotEmpty(data);
        }

        // Mau02: Kiểm tra trả về danh sách màu khi có dữ liệu phù hợp.
        [Fact]
        public void GetListMauSac_ReturnsColorList() {
            // Arrange
            var mau = new MauSac { MaMau = "#000000" };
            _context.MauSacs.Add(mau);
            _context.SaveChanges();

            var spt = new SanPhamBienThe { Id_SanPham = 1, Id_Mau = mau.Id };
            _context.SanPhamBienThes.Add(spt);
            _context.SaveChanges();

            var json = JObject.Parse(@"{ ""id_san_pham"": 1 }");

            // Act
            var result = _controller.getListMauSac(json) as JsonResult;

            // Assert
            Assert.NotNull(result);
            var data = Assert.IsAssignableFrom<IEnumerable<object>>(result.Value);
            Assert.NotEmpty(data);
        }

        // Mau03: Kiểm tra trả về dữ liệu kết hợp giữa màu sắc và loại khi có dữ liệu.
        [Fact]
        public async Task GetMauSacLoai_ReturnsCombinedData() {
            // Arrange
            var loai = new Loai { Ten = "Áo" };
            _context.Loais.Add(loai);
            await _context.SaveChangesAsync(); // để EF tự tạo Id

            var mau = new MauSac { Id_Loai = loai.Id, MaMau = "#FF0000" };
            _context.MauSacs.Add(mau);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetMauSacLoai();
            var actionResult = Assert.IsType<ActionResult<IEnumerable<TenMauLoai>>>(result);
            var data = Assert.IsAssignableFrom<IEnumerable<TenMauLoai>>(actionResult.Value);

            // Assert
            Assert.NotEmpty(data);
        }

        // Mau04: Kiểm tra cập nhật màu sắc thành công khi dữ liệu hợp lệ.
        [Fact]
        public async Task PutMauSac_ReturnsOk_WhenUpdateIsSuccessful() {
            // Arrange
            var mausac = new MauSac { MaMau = "#000000", Id_Loai = 1 };
            _context.MauSacs.Add(mausac);
            await _context.SaveChangesAsync();

            var upload = new UploadMauSac { MaMau = "#FFFFFF", Id_Loai = 2 };

            // Act
            var result = await _controller.PutMauSac(mausac.Id, upload);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);

            var updated = await _context.MauSacs.FindAsync(mausac.Id);
            Assert.Equal("#FFFFFF", updated.MaMau);
            Assert.Equal(2, updated.Id_Loai);
        }

        // Mau05: Kiểm tra trả về NotFound khi cập nhật màu sắc không tồn tại
        [Fact]
        public async Task PutMauSac_ReturnsNotFound_WhenMauSacDoesNotExist() {
            // Arrange
            var upload = new UploadMauSac { MaMau = "#FFFFFF", Id_Loai = 2 };
            int nonexistentId = 999;

            // Act
            var result = await _controller.PutMauSac(nonexistentId, upload);

            // Assert
            var notFound = Assert.IsType<NotFoundResult>(result);
        }

        // Mau06: Kiểm tra trả về đúng thông tin màu sắc khi ID tồn tại.
        [Fact]
        public async Task GetMauSac_ReturnsMauSac_WhenIdExists() {
            // Arrange
            var mausac = new MauSac { MaMau = "#ABC123", Id_Loai = 1 };
            _context.MauSacs.Add(mausac);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetMauSac(mausac.Id);

            // Assert
            var actionResult = Assert.IsType<ActionResult<MauSac>>(result);
            var value = Assert.IsType<MauSac>(actionResult.Value);
            Assert.Equal(mausac.Id, value.Id);
            Assert.Equal(mausac.MaMau, value.MaMau);
            Assert.Equal(mausac.Id_Loai, value.Id_Loai);
        }

        // Mau07: Kiểm tra trả về NotFound khi ID màu sắc không tồn tại.
        [Fact]
        public async Task GetMauSac_ReturnsNotFound_WhenIdDoesNotExist() {
            // Act
            var result = await _controller.GetMauSac(7777); // giả sử không có ID này

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(result.Result);
        }

        // Mau08: Kiểm tra thêm mới màu sắc thành công và tạo thông báo tương ứng.
        [Fact]
        public async Task PostMauSac_ReturnsOk_AndCreatesMauSacAndNotification() {
            // Arrange
            var upload = new UploadMauSac {
                MaMau = "#AABBCC",
                Id_Loai = 1
            };

            // Act
            var result = await _controller.PostMauSac(upload);

            // Assert
            var okResult = Assert.IsType<OkResult>(result.Result);

            var mausac = _context.MauSacs.FirstOrDefault(m => m.MaMau == "#AABBCC");
            Assert.NotNull(mausac);
            Assert.Equal(upload.Id_Loai, mausac.Id_Loai);

            var notification = _context.Notifications.FirstOrDefault(n => n.TenSanPham == "#AABBCC");
            Assert.NotNull(notification);
            Assert.Equal("Add", notification.TranType);
        }

        // Mau09: Kiểm tra xóa màu sắc thành công, dữ liệu bị xóa và tạo thông báo tương ứng.
        [Fact]
        public async Task DeleteMauSac_ReturnsOk_AndDeletesMauSacAndCreatesNotification() {
            // Arrange
            var mausac = new MauSac { MaMau = "#FAFAFA", Id_Loai = 1 };
            _context.MauSacs.Add(mausac);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.DeleteMauSac(mausac.Id);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);

            var deleted = await _context.MauSacs.FindAsync(mausac.Id);
            Assert.Null(deleted);

            var notification = _context.Notifications.FirstOrDefault(n => n.TenSanPham == "#FAFAFA");
            Assert.NotNull(notification);
            Assert.Equal("Delete", notification.TranType);
        }

        // Mau10: Kiểm tra trả về NotFound khi xóa màu sắc với ID không tồn tại.
        [Fact]
        public async Task DeleteMauSac_ReturnsNotFound_WhenIdDoesNotExist() {
            // Act
            var result = await _controller.DeleteMauSac(999); // ID không tồn tại

            // Assert
            var notFound = Assert.IsType<NotFoundResult>(result);
        }
    }
}
