using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helper.SignalR;
using API.Models;
using API.Helpers;
using Xunit;
using Microsoft.AspNetCore.SignalR;

namespace API.Test
{
    public class HoaDonsControllerTest
    {
        private readonly DPContext _context;
        private readonly Mock<IHubContext<BroadcastHub, IHubClient>> _hubContextMock;
        private readonly Mock<IDataConnector> _connectorMock;
        private readonly HoaDonsController _controller;
        private readonly string _fixedUserId = "a3b5c7d9-e1f2-4a5b-8c3d-123456789abc"; // GUID cố định

        public HoaDonsControllerTest()
        {
            // Thiết lập InMemory database
            var options = new DbContextOptionsBuilder<DPContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new DPContext(options);

            // Mock IHubContext và IDataConnector
            _hubContextMock = new Mock<IHubContext<BroadcastHub, IHubClient>>();

            // Thiết lập mock cho IHubContext
            var clientsMock = new Mock<IHubClients<IHubClient>>();
            var clientMock = new Mock<IHubClient>();

            // Thiết lập hành vi cho Clients.All
            clientsMock.Setup(clients => clients.All).Returns(clientMock.Object);
            _hubContextMock.Setup(hub => hub.Clients).Returns(clientsMock.Object);

            // Thiết lập hành vi cho BroadcastMessage (nếu cần kiểm tra giá trị trả về)
            clientMock.Setup(client => client.BroadcastMessage()).Returns(Task.CompletedTask);

            _connectorMock = new Mock<IDataConnector>();

            // Khởi tạo controller
            _controller = new HoaDonsController(_context, _hubContextMock.Object, _connectorMock.Object);

            // Thiết lập dữ liệu ban đầu
            SeedData();
        }

        private void SeedData()
        {
            // Thêm dữ liệu giả lập với idUser dạng GUID cố định
            var user = new AppUser { Id = _fixedUserId, FirstName = "John", LastName = "Doe" };
            var hoaDon = new HoaDon { Id = 1, Id_User = _fixedUserId, GhiChu = "Test", NgayTao = DateTime.Now, TrangThai = 0, TongTien = 100000 };
            var sanPham = new SanPham { Id = 1, Ten = "Áo thun", GiaBan = 50000 };
            var sanPhamBienThe = new SanPhamBienThe { Id = 1, Id_SanPham = 1, SizeId = 1, Id_Mau = 1, SoLuongTon = 10 };
            var size = new Size { Id = 1, TenSize = "M" };
            var mau = new MauSac { Id = 1, MaMau = "Đen" };
            var chiTietHoaDon = new ChiTietHoaDon { Id = 1, Id_HoaDon = 1, Id_SanPhamBienThe = 1, Soluong = 2, GiaBan = 50000, ThanhTien = 100000 };
            var maGiamGia = new MaGiamGia { Id = 1, Code = "DISCOUNT10", SoTienGiam = 10 };
            var cart = new Cart { CartID = 1, UserID = _fixedUserId, SanPhamId = 1, Id_SanPhamBienThe = 1, SoLuong = 2, Gia = 50000, Size = "M", Mau = "Đen" };

            _context.AppUsers.Add(user);
            _context.HoaDons.Add(hoaDon);
            _context.SanPhams.Add(sanPham);
            _context.SanPhamBienThes.Add(sanPhamBienThe);
            _context.Sizes.Add(size);
            _context.MauSacs.Add(mau);
            _context.ChiTietHoaDons.Add(chiTietHoaDon);
            _context.MaGiamGias.Add(maGiamGia);
            _context.Carts.Add(cart);
            _context.SaveChanges();
        }

        // HoaDon01
        [Fact]
        public async Task AllHoaDons_ReturnsList_WhenDataExists()
        {
            // Gọi controller 
            var result = await _controller.AllHoaDons();

            // Kiểm tra kết quả
            var okResult = Assert.IsType<ActionResult<IEnumerable<HoaDonUser>>>(result);
            var hoaDons = Assert.IsAssignableFrom<IEnumerable<HoaDonUser>>(okResult.Value);
            Assert.Single(hoaDons);
            Assert.Equal("John Doe", hoaDons.First().FullName);
        }
        // HoaDon02
        [Fact]
        public async Task AllHoaDons_ReturnsEmptyList_WhenNoData()
        {
            // Xóa toàn bộ bản ghi hóa đơn 
            _context.HoaDons.RemoveRange(_context.HoaDons);
            _context.SaveChanges();

            // Gọi Controller
            var result = await _controller.AllHoaDons();

            // Kiểm  tra kết quả 
            var okResult = Assert.IsType<ActionResult<IEnumerable<HoaDonUser>>>(result);
            var hoaDons = Assert.IsAssignableFrom<IEnumerable<HoaDonUser>>(okResult.Value);
            Assert.Empty(hoaDons);
        }

        // HoaDon03
        [Fact]
        public async Task HoaDonDetailAsync_ReturnsHoaDon_WhenIdIsValid()
        {
            // Tạo một bản ghi 
            var motHoaDon = new MotHoaDon { Id = 1, DiaChi = "Test" };
            _connectorMock.Setup(c => c.HoaDonDetailAsync(1)).ReturnsAsync(motHoaDon);

            // Gọi Controller
            var result = await _controller.HoaDonDetailAsync(1);

            // Kiểm tra kết quả có như mong muốn
            var okResult = Assert.IsType<ActionResult<MotHoaDon>>(result);
            var returnedHoaDon = Assert.IsType<MotHoaDon>(okResult.Value);
            Assert.Equal(1, returnedHoaDon.Id);
        }

        // HoaDon04
        [Fact]
        public async Task HoaDonDetailAsync_ReturnsNull_WhenIdDoesNotExist()
        {
            // SetId về null ( nghĩa là không có)
            _connectorMock.Setup(c => c.HoaDonDetailAsync(999)).ReturnsAsync((MotHoaDon)null);

            // Gọi Controller
            var result = await _controller.HoaDonDetailAsync(999);

            // Kiểm tra kết quả có như mong muốn
            var okResult = Assert.IsType<ActionResult<MotHoaDon>>(result);
            Assert.Null(okResult.Value);
        }

        // HoaDon05
        [Fact]
        public async Task ChitietHoaDon_ReturnsHoaDonWithUser_WhenIdIsValid()
        {
            // Gọi Controller
            var result = await _controller.ChitietHoaDon(1);

            // Kiểm tra kết quả có như mong muốn
            var jsonResult = Assert.IsType<JsonResult>(result);
            var hoaDon = Assert.IsType<HoaDon>(jsonResult.Value);
            Assert.Equal(1, hoaDon.Id);
            Assert.NotNull(hoaDon.User);
            Assert.Equal("John Doe", hoaDon.User.FirstName + " " + hoaDon.User.LastName);
        }
        // HoaDon13
        [Fact]
        public async Task ChitietHoaDon_ReturnsNotFound_WhenIdDoesNotExist()
        {
            // Gọi Controller
            var result = await _controller.ChitietHoaDon(999);

            // Kiểm tra kết quả có như mong muốn
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);

            // Kiểm tra anonymous type
            var error = notFoundResult.Value;
            Assert.NotNull(error);

            // Sử dụng reflection để lấy giá trị của thuộc tính "message"
            var messageProperty = error.GetType().GetProperty("message");
            Assert.NotNull(messageProperty);
            var messageValue = messageProperty.GetValue(error) as string;
            Assert.Equal($"Không tìm thấy hóa đơn với ID 999", messageValue);
        }

        // HoaDon06
        [Fact]
        public async Task ListHoaDon_ReturnsList_WhenUserIdIsValid()
        {
            // Tạo mới bản ghi
            var userDto = new UserDto { idUser = _fixedUserId };

            // Gọi Controller
            var result = await _controller.ListHoaDon(userDto);

            // Kiểm tra kết quả có như mong muốn
            var jsonResult = Assert.IsType<JsonResult>(result);
            var hoaDons = Assert.IsAssignableFrom<List<HoaDon>>(jsonResult.Value);
            Assert.Single(hoaDons);
            Assert.Equal(1, hoaDons.First().Id);
        }
        // HoaDon07
        [Fact]
        public async Task ListHoaDon_ReturnsEmptyList_WhenNoHoaDonForUser()
        {
            // Tạo mới bản ghi
            var userDto = new UserDto { idUser = "non-existent-user-id" };

            // Gọi Controller
            var result = await _controller.ListHoaDon(userDto);

            // Kiểm tra kết quả có như mong muốn
            var jsonResult = Assert.IsType<JsonResult>(result);
            var hoaDons = Assert.IsAssignableFrom<List<HoaDon>>(jsonResult.Value);
            Assert.Empty(hoaDons);
        }

        // HoaDon14
        [Fact]
        public async Task SuaTrangThai_UpdatesStatus_WhenIdIsValid()
        {
            // Tạo một bản ghi để kiểm tra
            var hoaDonUser = new HoaDonUser { TrangThai = 1 };

            // Gọi Controller
            var result = await _controller.SuaTrangThai(1, hoaDonUser);

            // Kiểm tra kết quả có như mong muốn
            Assert.IsType<OkResult>(result);
            var updatedHoaDon = await _context.HoaDons.FindAsync(1);
            Assert.Equal(1, updatedHoaDon.TrangThai);
        }
        // HoaDon15
        [Fact]
        public async Task SuaTrangThai_ReturnsNotFound_WhenIdDoesNotExist()
        {
            // Tạo một bản ghi để kiểm tra 
            var hoaDonUser = new HoaDonUser { TrangThai = 1 };

            // Gọi Controller
            var result = await _controller.SuaTrangThai(999, hoaDonUser);

            // Kiểm tra kết quả có như mong muốn
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);

            // Kiểm tra anonymous type
            var error = notFoundResult.Value;
            Assert.NotNull(error);

            var messageProperty = error.GetType().GetProperty("message");
            Assert.NotNull(messageProperty);
            var messageValue = messageProperty.GetValue(error) as string;
            Assert.Equal($"Không tìm thấy hóa đơn với ID 999", messageValue);
        }

        // HoaDon08
        [Fact]
        public async Task GetChiTietHoaDonSanPhamBienTheViewModel_ReturnsList_WhenIdIsValid()
        {
            // Gọi Controller
            var result = await _controller.GetChiTietHoaDonSanPhamBienTheViewModel(1);

            // Kiểm tra kết quả có như mong muốn
            var okResult = Assert.IsType<ActionResult<IEnumerable<ChiTietHoaDonSanPhamBienTheViewModel>>>(result);
            var chiTietHoaDons = Assert.IsAssignableFrom<IEnumerable<ChiTietHoaDonSanPhamBienTheViewModel>>(okResult.Value);
            Assert.Single(chiTietHoaDons);
            Assert.Equal("Áo thun", chiTietHoaDons.First().TenSanPham);
        }
        // HoaDon09
        [Fact]
        public async Task GetChiTietHoaDonSanPhamBienTheViewModel_ReturnsEmptyList_WhenNoData()
        {
            // Gọi Controller
            var result = await _controller.GetChiTietHoaDonSanPhamBienTheViewModel(999);

            // Kiểm tra kết quả có như mong muốn
            var okResult = Assert.IsType<ActionResult<IEnumerable<ChiTietHoaDonSanPhamBienTheViewModel>>>(result);
            var chiTietHoaDons = Assert.IsAssignableFrom<IEnumerable<ChiTietHoaDonSanPhamBienTheViewModel>>(okResult.Value);
            Assert.Empty(chiTietHoaDons);
        }

        // HoaDon10
        [Fact]
        public async Task MaGiamGia_ReturnsList_WhenDataExists()
        {
            // Gọi Controller
            var result = await _controller.MaGiamGia();

            // Kiểm tra kết quả có như mong muốn
            var okResult = Assert.IsType<ActionResult<IEnumerable<MaGiamGia>>>(result);
            var maGiamGias = Assert.IsAssignableFrom<IEnumerable<MaGiamGia>>(okResult.Value);
            Assert.Single(maGiamGias);
            Assert.Equal("DISCOUNT10", maGiamGias.First().Code);
        }
        // HoaDon11
        [Fact]
        public async Task MaGiamGia_ReturnsEmptyList_WhenNoData()
        {
            // Tạo một bản ghi để kiểm tra
            _context.MaGiamGias.RemoveRange(_context.MaGiamGias);
            _context.SaveChanges();

            // Gọi Controller
            var result = await _controller.MaGiamGia();

            // Kiểm tra kết quả có như mong muốn
            var okResult = Assert.IsType<ActionResult<IEnumerable<MaGiamGia>>>(result);
            var maGiamGias = Assert.IsAssignableFrom<IEnumerable<MaGiamGia>>(okResult.Value);
            Assert.Empty(maGiamGias);
        }

        // HoaDon16
        [Fact]
        public async Task TaoHoaDon_CreatesHoaDon_WhenInputIsValid()
        {
            // Tạo một bản ghi để kiểm tra
            var hoaDon = new HoaDon
            {
                Id_User = _fixedUserId,
                GhiChu = "New Order",
                Tinh = "Hà Nội",
                Huyen = "Ba Đình",
                Xa = "Phúc Xá",
                DiaChi = "123 Đường Láng",
                TongTien = 100000
            };

            // Gọi Controller
            var result = await _controller.TaoHoaDon(hoaDon);

            // Kiểm tra kết quả có như mong muốn
            var okResult = Assert.IsType<ActionResult<HoaDon>>(result);
            var createdHoaDon = Assert.IsType<HoaDon>(okResult.Value);
            Assert.Equal("New Order", createdHoaDon.GhiChu);
            Assert.Empty(_context.Carts); // Giỏ hàng đã bị xóa
            Assert.Equal(8, _context.SanPhamBienThes.Find(1).SoLuongTon); // Số lượng tồn giảm
        }
        // HoaDon17
        [Fact]
        public async Task TaoHoaDon_CreatesHoaDon_WhenCartIsEmpty()
        {
            // Tạo một bản ghi để kiểm tra
            _context.Carts.RemoveRange(_context.Carts);
            _context.SaveChanges();
            var hoaDon = new HoaDon
            {
                Id_User = _fixedUserId,
                GhiChu = "New Order",
                Tinh = "Hà Nội",
                Huyen = "Ba Đình",
                Xa = "Phúc Xá",
                DiaChi = "123 Đường Láng",
                TongTien = 100000
            };

            // Gọi Controller
            var result = await _controller.TaoHoaDon(hoaDon);

            // Kiểm tra kết quả có như mong muốn
            var okResult = Assert.IsType<ActionResult<HoaDon>>(result);
            var createdHoaDon = Assert.IsType<HoaDon>(okResult.Value);
            Assert.Equal("New Order", createdHoaDon.GhiChu);
            Assert.Empty(_context.ChiTietHoaDons.Where(ct => ct.Id_HoaDon == createdHoaDon.Id));
        }

        // HoaDon12
        [Fact]
        public async Task DeleteHoaDons_DeletesHoaDon_WhenIdIsValid()
        {
            // Gọi Controller
            var result = await _controller.DeleteHoaDons(1);

            // Kiểm tra kết quả có như mong muốn
            Assert.IsType<OkResult>(result);
            Assert.Null(_context.HoaDons.Find(1));
            Assert.Empty(_context.ChiTietHoaDons.Where(ct => ct.Id_HoaDon == 1));
        }
    }
}