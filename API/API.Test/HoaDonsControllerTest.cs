using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helper.SignalR;
using API.Models;
using API.Test;
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
    public class HoaDonsControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly Mock<IHubContext<BroadcastHub, IHubClient>> _hubContextMock;
        private readonly Mock<IDataConnector> _connectorMock;
        private readonly HoaDonsController _controller;
        private readonly string _fixedUserId = "a3b5c7d9-e1f2-4a5b-8c3d-123456789abc"; // GUID cố định

        public HoaDonsControllerTests() : base()
        {
            // Khởi tạo InMemory Database từ TestBase
            _context = new DPContext(_options);

            // Mock IHubContext để giả lập BroadcastHub
            _hubContextMock = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClientProxy.Setup(client => client.BroadcastMessage()).Returns(Task.CompletedTask);

            // Mock IDataConnector
            _connectorMock = new Mock<IDataConnector>();

            // Khởi tạo controller
            _controller = new HoaDonsController(_context, _hubContextMock.Object, _connectorMock.Object);
        }
        // HoaDon01: Lấy danh sách toàn bộ hóa đơn ngay sau khi có dữ liệu được thêm mới 
        [Fact]
        public async Task HoaDon01_AllHoaDons_ReturnsList_WhenAddingNewData()
        {
            // Arrange - Chuẩn bị dữ liệu
            var user = new AppUser { Id = _fixedUserId, FirstName = "John", LastName = "Doe" };
            var hoaDon = new HoaDon
            {
                Id_User = _fixedUserId,
                GhiChu = "Test HoaDon",
                NgayTao = DateTime.Now,
                TrangThai = 0,
                TongTien = 100000
            };
            _context.AppUsers.Add(user);
            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync();

            // Act - Gọi API
            var result = await _controller.AllHoaDons();

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<HoaDonUser>>>(result);
            var hoaDons = Assert.IsAssignableFrom<IEnumerable<HoaDonUser>>(actionResult.Value);
            Assert.NotEmpty(hoaDons); // Kiểm tra danh sách không rỗng

            // Kiểm tra xem danh sách có chứa hóa đơn vừa thêm hay không
            var addedHoaDon = hoaDons.FirstOrDefault(h => h.Id == hoaDon.Id);
            Assert.NotNull(addedHoaDon); // Kiểm tra rằng hóa đơn vừa thêm tồn tại trong danh sách
            Assert.Equal("Test HoaDon", addedHoaDon.GhiChu); // Kiểm tra GhiChu
            Assert.Equal(100000, addedHoaDon.TongTien); // Kiểm tra TongTien
        }

        // HoaDon02: Lấy chi tiết hóa đơn khi có thêm mới MotHoaDon và xem sau khi them chi tiết đó có lấy được ra hay không
        [Fact]
        public async Task HoaDon02_HoaDonDetailAsync_ReturnsHoaDon_WhenIdIsValid()
        {
            // Arrange - Thiết lập mock cho IDataConnector
            var motHoaDon = new MotHoaDon { DiaChi = "TestHoaDon02" };
            _connectorMock.Setup(c => c.HoaDonDetailAsync(1)).ReturnsAsync(motHoaDon);

            // Act - Gọi API
            var result = await _controller.HoaDonDetailAsync(1);

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<MotHoaDon>>(result);
            var returnedHoaDon = Assert.IsType<MotHoaDon>(actionResult.Value);
            Assert.Equal(motHoaDon.Id, returnedHoaDon.Id); // Kiểm tra Id hóa đơn
            Assert.Equal("TestHoaDon02", returnedHoaDon.DiaChi); // Kiểm tra địa chỉ
        }

        // HoaDon03: Lấy chi tiết hóa đơn khi có lỗi bất ngờ truy vấn DB thât bại 
        [Fact]
        public async Task HoaDon03_ChitietHoaDon_ReturnsInternalServerError_WhenExceptionOccurs()
        { 

            // Arrange - Tạo DPContext với connection string sai để gây lỗi
            var badOptions = new DbContextOptionsBuilder<DPContext>()
                .UseSqlServer("Server=INVALID_SERVER;Database=EFashionShop;Integrated Security=True")
                .Options;

            var contextWithError = new DPContext(badOptions);
            var controllerWithError = new HoaDonsController(contextWithError, _hubContextMock.Object, _connectorMock.Object);

            // Act - Gọi API
            var result = await controllerWithError.ChitietHoaDon(1);

            // Assert - Kiểm tra kết quả
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode); // Kiểm tra mã lỗi 500
            var error = statusCodeResult.Value;
            var messageProperty = error.GetType().GetProperty("message");
            var messageValue = messageProperty.GetValue(error) as string;
            Assert.Equal("Đã xảy ra lỗi khi lấy chi tiết hóa đơn", messageValue); // Kiểm tra thông báo lỗi
        }

        // HoaDon04 : Kiểm tra xem có trả đúng thông báo khi id không hợp lệ 
        [Fact]
        public async Task HoaDon04_ChitietHoaDon_ReturnsNotFound_WhenHoaDonNotFound()
        {
            // Act: Gọi API ChitietHoaDon với ID = 999
            var result = await _controller.ChitietHoaDon(999);

            // Assert: Kiểm tra kết quả trả về
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var error = Assert.IsType<Dictionary<string, string>>(notFoundResult.Value);
            Assert.Equal("Không tìm thấy hóa đơn bạn cần tìm", error["massage"]); // Xác nhận thông báo lỗi
        }

        // HoaDon05: Kiểm tra ChitietHoaDon Sau khi them mới 1 hóa đơn thì có có trong DB hay không.
        [Fact]
        public async Task HoaDon05_ChitietHoaDon_ReturnsJson_WhenHoaDonFound()
        {
            // Arrange: Thêm dữ liệu mẫu vào cơ sở dữ liệu
            var user = new AppUser {  FirstName = "John", LastName = "Doe" };
            var hoaDon = new HoaDon { Id_User = user.Id, GhiChu = "Test" };
            _context.AppUsers.Add(user);
            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync();

            // Act: Gọi API ChitietHoaDon
            var result = await _controller.ChitietHoaDon(hoaDon.Id);

            // Assert: Kiểm tra kết quả trả về
            var jsonResult = Assert.IsType<JsonResult>(result);
            var returnedHoaDon = Assert.IsType<HoaDon>(jsonResult.Value);
            Assert.Equal(hoaDon.Id, returnedHoaDon.Id); // Xác nhận ID hóa đơn
            Assert.Equal("Test", returnedHoaDon.GhiChu); // Xác nhận ghi chú
            Assert.NotNull(returnedHoaDon.User); // Xác nhận có thông tin người dùng
            Assert.Equal("John Doe", $"{returnedHoaDon.User.FirstName} {returnedHoaDon.User.LastName}"); // Xác nhận tên người dùng
        }


        // HoaDon06: Lấy danh sách hóa đơn của người dùng khi Id người dùng hợp lệ 
        [Fact]
        public async Task HoaDon06_ListHoaDon_ReturnsList()
        {
            // Arrange: Tạo UserDto với ID người dùng
            var userDto = new UserDto { idUser = _fixedUserId };

            // Act: Gọi API ListHoaDon
            var result = await _controller.ListHoaDon(userDto);

            // Assert: Kiểm tra kết quả trả về
            var jsonResult = Assert.IsType<JsonResult>(result);
            var hoaDons = Assert.IsAssignableFrom<List<HoaDon>>(jsonResult.Value);
            Assert.Empty(hoaDons); // Xác nhận danh sách rỗng vì không có dữ liệu
        }

        // HoaDon07: Cập nhật trạng thái hóa đơn khi Id hợp lệ
        [Fact]
        public async Task HoaDon07_SuaTrangThai_UpdatesStatus_WhenIdIsValid()
        {
            // Arrange - Chuẩn bị dữ liệu
            var user = new AppUser { FirstName = "John", LastName = "Doe" };
            var hoaDon = new HoaDon { Id_User = user.Id, GhiChu = "Test", NgayTao = DateTime.Now, TrangThai = 0, TongTien = 100000 };
            _context.AppUsers.Add(user);
            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync();

            var hoaDonUser = new HoaDonUser { TrangThai = 1 };

            // Act - Gọi API
            var result = await _controller.SuaTrangThai(hoaDon.Id, hoaDonUser);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<OkResult>(result); // Kiểm tra API trả về thông báo thành công
            var updatedHoaDon = await _context.HoaDons.FindAsync(hoaDon.Id);
            Assert.Equal(1, updatedHoaDon.TrangThai); // Kiểm tra trạng thái đã được cập nhật

        }

        // HoaDon08: Cập nhật trạng thái hóa đơn khi Id không tồn tại
        [Fact]
        public async Task HoaDon08_SuaTrangThai_ReturnsNotFound_WhenIdDoesNotExist()
        {
            // Arrange - Chuẩn bị dữ liệu
            var hoaDonUser = new HoaDonUser { TrangThai = 1 };

            // Act - Gọi API
            var result = await _controller.SuaTrangThai(999, hoaDonUser);

            // Assert - Kiểm tra kết quả
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var error = notFoundResult.Value;
            var messageProperty = error.GetType().GetProperty("message");
            var messageValue = messageProperty.GetValue(error) as string;
            Assert.Equal("Không thể sửa hóa đơn mà bạn đã sửa", messageValue); // Kiểm tra thông báo lỗi
        }

        // HoaDon09: Cập nhật trạng thái hóa đơn khi có lỗi bất ngờ
        [Fact]
        public async Task HoaDon09_SuaTrangThai_ReturnsInternalServerError_WhenExceptionOccurs()
        {
            // Arrange - Tạo DPContext với connection string sai để gây lỗi
            var badOptions = new DbContextOptionsBuilder<DPContext>()
                .UseSqlServer("Server=INVALID_SERVER;Database=EFashionShop;Integrated Security=True")
                .Options;

            var contextWithError = new DPContext(badOptions);
            var controllerWithError = new HoaDonsController(contextWithError, _hubContextMock.Object, _connectorMock.Object);

            // Act - Gọi API
            var result = await controllerWithError.SuaTrangThai(1, new HoaDonUser { TrangThai = 1 });

            // Assert - Kiểm tra kết quả
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode); // Kiểm tra mã lỗi 500
            var error = statusCodeResult.Value;
            var messageProperty = error.GetType().GetProperty("message");
            var messageValue = messageProperty.GetValue(error) as string;
            Assert.Equal("Đã xảy ra lỗi khi cập nhật trạng thái hóa đơn", messageValue); // Kiểm tra thông báo lỗi

        }

        // HoaDon10: Lấy danh sách chi tiết hóa đơn sản phẩm biến thể khi Id hợp lệ
        [Fact]
        public async Task HoaDon10_GetChiTietHoaDonSanPhamBienTheViewModel_ReturnsList_WhenIdIsValid()
        {

            // Arrange - Chuẩn bị dữ liệu
            // Bước 1: Thêm các bản ghi không phụ thuộc (SanPham, Size, MauSac)
            var sanPham = new SanPham { Ten = "Áo thun", GiaBan = 50000 };
            var size = new Size { TenSize = "M" };
            var mau = new MauSac { MaMau = "Đen" };

            _context.SanPhams.Add(sanPham);
            _context.Sizes.Add(size);
            _context.MauSacs.Add(mau);
            await _context.SaveChangesAsync(); // Lưu để sinh Id cho SanPham, Size, MauSac

            // Bước 2: Thêm SanPhamBienThe (phụ thuộc vào SanPham, Size, MauSac)
            var sanPhamBienThe = new SanPhamBienThe
            {
                Id_SanPham = sanPham.Id,
                SizeId = size.Id,
                Id_Mau = mau.Id,
                SoLuongTon = 10
            };
            _context.SanPhamBienThes.Add(sanPhamBienThe);
            await _context.SaveChangesAsync(); // Lưu để sinh Id cho SanPhamBienThe

            // Bước 3: Thêm HoaDon và ChiTietHoaDon (phụ thuộc vào SanPhamBienThe)
            var hoaDon = new HoaDon
            {
                GhiChu = "Test",
                NgayTao = DateTime.Now,
                TrangThai = 0,
                TongTien = 100000
            };
            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync(); // Lưu để sinh Id cho HoaDon

            var chiTietHoaDon = new ChiTietHoaDon
            {
                Id_HoaDon = hoaDon.Id,
                Id_SanPhamBienThe = sanPhamBienThe.Id,
                Soluong = 2,
                GiaBan = 50000,
                ThanhTien = 100000
            };
            _context.ChiTietHoaDons.Add(chiTietHoaDon);
            await _context.SaveChangesAsync(); // Lưu ChiTietHoaDon

            // Act - Gọi API
            var result = await _controller.GetChiTietHoaDonSanPhamBienTheViewModel(chiTietHoaDon.Id);

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<ChiTietHoaDonSanPhamBienTheViewModel>>>(result);
            var chiTietHoaDons = Assert.IsAssignableFrom<IEnumerable<ChiTietHoaDonSanPhamBienTheViewModel>>(actionResult.Value);
            Assert.NotEmpty(chiTietHoaDons); // Kiểm tra danh sách không rỗng
            // Kiểm tra xem danh sách có chứa bản ghi ChiTietHoaDon vừa tạo hay không
            var matchingChiTiet = chiTietHoaDons.FirstOrDefault(ct => ct.IdCTHD == chiTietHoaDon.Id);
            Assert.NotNull(matchingChiTiet); // Kiểm tra rằng bản ghi vừa tạo tồn tại trong danh sách
            Assert.Equal(chiTietHoaDon.Soluong, matchingChiTiet.SoLuong); // Kiểm tra số lượng
            Assert.Equal(chiTietHoaDon.GiaBan, matchingChiTiet.GiaBan); // Kiểm tra giá bán
            Assert.Equal(chiTietHoaDon.ThanhTien, matchingChiTiet.ThanhTien); // Kiểm tra thành tiền

            // Kiểm tra các thuộc tính khác
            Assert.Equal("Áo thun", matchingChiTiet.TenSanPham); // Kiểm tra tên sản phẩm
            Assert.Equal("M", matchingChiTiet.TenSize); // Kiểm tra size
            Assert.Equal("Đen", matchingChiTiet.TenMau); // Kiểm tra màu
        }

        // HoaDon11: Kiểm tra xem hóa đơn vừa tạo có ở trong hệ thống không 
        [Fact]
        public async Task HoaDon11_TaoHoaDon_CreatesHoaDon_WhenInputIsValid()
        {
            // Arrange - Chuẩn bị dữ liệu
            // Bước 1: Thêm các bản ghi không phụ thuộc (SanPham, Size, MauSac)
            var sanPham = new SanPham { Ten = "Áo thun", GiaBan = 50000 };
            var size = new Size { TenSize = "M" };
            var mau = new MauSac { MaMau = "Đen" };

            _context.SanPhams.Add(sanPham);
            _context.Sizes.Add(size);
            _context.MauSacs.Add(mau);
            await _context.SaveChangesAsync(); // Lưu để sinh Id cho SanPham, Size, MauSac

            // Bước 2: Thêm SanPhamBienThe (phụ thuộc vào SanPham, Size, MauSac)
            var sanPhamBienThe = new SanPhamBienThe
            {
                Id_SanPham = sanPham.Id,
                SizeId = size.Id,
                Id_Mau = mau.Id,
                SoLuongTon = 10
            };
            _context.SanPhamBienThes.Add(sanPhamBienThe);

            await _context.SaveChangesAsync(); // Lưu để sinh Id cho SanPhamBienThe

            // Bước 3: Thêm HoaDon và ChiTietHoaDon (phụ thuộc vào SanPhamBienThe)
            var hoaDon = new HoaDon
            {
                GhiChu = "New Order",
                Tinh = "Hà Nội",
                Huyen = "Ba Đình",
                Xa = "Phúc Xá",
                DiaChi = "123 Đường Láng",
                TongTien = 100000,
            };
            _context.HoaDons.Add(hoaDon);
            var chiTietHoaDon = new ChiTietHoaDon
            {
                Id_HoaDon = hoaDon.Id,
                Id_SanPhamBienThe = sanPhamBienThe.Id,
                Soluong = 2,
                GiaBan = 50000,
                ThanhTien = 100000
            };
            _context.ChiTietHoaDons.Add(chiTietHoaDon);
            await _context.SaveChangesAsync(); // Lưu ChiTietHoaDon


            // Act - Gọi API
            var result = await _controller.TaoHoaDon(hoaDon);

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<HoaDon>>(result);
            var createdHoaDon = Assert.IsType<HoaDon>(actionResult.Value);
            Assert.Equal("New Order", createdHoaDon.GhiChu); // Kiểm tra ghi chú hóa đơn
            Assert.Equal(0, createdHoaDon.TrangThai); // Kiểm tra trạng thái mặc định
            Assert.Equal(8, _context.SanPhamBienThes.Find(sanPhamBienThe.Id).SoLuongTon); // Kiểm tra số lượng tồn giảm (10 - 2 = 8)
            Assert.NotNull(_context.NotificationCheckouts.FirstOrDefault(n => n.ThongBaoMaDonHang == createdHoaDon.Id)); // Kiểm tra thông báo được tạo
        }

        // HoaDon12: Xóa hóa đơn khi Id hợp lệ
        [Fact]
        public async Task HoaDon12_DeleteHoaDons_DeletesHoaDon_WhenIdIsValid()
        {

            // Arrange - Chuẩn bị dữ liệu
            var user = new AppUser { FirstName = "John", LastName = "Doe" };
            var hoaDon = new HoaDon { Id_User = user.Id, GhiChu = "Test", NgayTao = DateTime.Now, TrangThai = 0, TongTien = 100000 };
            var chiTietHoaDon = new ChiTietHoaDon { Id_HoaDon = 1, Id_SanPhamBienThe = 1, Soluong = 2, GiaBan = 50000, ThanhTien = 100000 };
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();
            _context.HoaDons.Add(hoaDon);
            _context.ChiTietHoaDons.Add(chiTietHoaDon);
            await _context.SaveChangesAsync();

            // Act - Gọi API
            var result = await _controller.DeleteHoaDons(hoaDon.Id);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<OkResult>(result); // Kiểm tra API trả về thông báo thành công
            Assert.Null(_context.HoaDons.Find(hoaDon.Id)); // Kiểm tra hóa đơn đã bị xóa
            Assert.Empty(_context.ChiTietHoaDons.Where(ct => ct.Id_HoaDon == hoaDon.Id)); // Kiểm tra chi tiết hóa đơn đã bị xóa
        }

        // Test Case: HoaDon13
        [Fact]
        public async Task HoaDon13_MaGiamGia_ReturnsList()
        {
   
            // Act: Gọi API MaGiamGia
            var result = await _controller.MaGiamGia();

            // Assert: Kiểm tra kết quả trả về
            var actionResult = Assert.IsType<ActionResult<IEnumerable<MaGiamGia>>>(result);
            var maGiamGias = Assert.IsAssignableFrom<IEnumerable<MaGiamGia>>(actionResult.Value);
            Assert.NotEmpty(maGiamGias); // Xác nhận danh sách không rỗng 
        }

    }
}