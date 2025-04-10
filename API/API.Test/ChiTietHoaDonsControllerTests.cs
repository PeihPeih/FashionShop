using API.Controllers;
using API.Data;
using API.Dtos;
using API.Models;
using API.Test;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace API.Test
{
    public class ChiTietHoaDonsControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly ChiTietHoaDonsController _controller;

        public ChiTietHoaDonsControllerTests() : base()
        {
            // Khởi tạo InMemory Database để test
            _context = new DPContext(_options);
            _controller = new ChiTietHoaDonsController(_context);
        }

        // ChiTietHD01: Lấy danh sách chi tiết hóa đơn khi có dữ liệu
        [Fact]
        public async Task ChiTietHD01_GetChiTetHoaDons_ReturnsList_WhenDataExists()
        {
            // Arrange - Chuẩn bị dữ liệu
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
            var result = await _controller.ChiTetHoaDons();

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<ChiTietHoaDon>>>(result);
            var data = Assert.IsAssignableFrom<IEnumerable<ChiTietHoaDon>>(actionResult.Value);
            Assert.NotEmpty(data); // Kiểm tra danh sách không rỗng
            var firstItem = data.FirstOrDefault(x => x.Id == chiTietHoaDon.Id);
            Assert.Equal(50000, firstItem.GiaBan);
            Assert.Equal(2, firstItem.Soluong);
        }

        // ChiTietHD02: Lấy chi tiết hóa đơn theo Id_HoaDon khi Id tồn tại
        [Fact]
        public async Task ChiTietHD02_ChitietHoaDon_ReturnsDetails_WhenIdExists()
        { 
            // Arrange - Chuẩn bị dữ liệu
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
            var result = await _controller.ChitietHoaDon(hoaDon.Id);

            // Assert - Kiểm tra kết quả
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = Assert.IsType<ChiTietHoaDon>(jsonResult.Value);
            Assert.Equal(50000, data.GiaBan);
            Assert.Equal(2, data.Soluong);
        }

        // ChiTietHD03: Lấy chi tiết hóa đơn theo Id_HoaDon khi Id không tồn tại
        [Fact]
        public async Task ChiTietHD03_ChitietHoaDon_ReturnsNull_WhenIdDoesNotExist()
        {
            // Mục tiêu: 
            // Input: (không tồn tại trong database).
            // Expected Output: Trả về null (không tìm thấy chi tiết hóa đơn).

            // Act - Gọi API với Id_HoaDon không tồn tại
            var result = await _controller.ChitietHoaDon(999);

            var actionResult = Assert.IsType<ActionResult<MotHoaDon>>(result);
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(actionResult.Result);
            Assert.Equal("Không tồn tại bản ghi này trong hệ thống", notFoundResult.Value);
        }

        // ChiTietHD04: Hủy hóa đơn khi Id tồn tại
        [Fact]
        public async Task ChiTietHD04_HuyDon_Success_WhenIdExists()
        {
            // Arrange - Chuẩn bị dữ liệu
            var user = new AppUser { FirstName = "John", LastName = "Doe" };
            var hoaDon = new HoaDon
            {
                Id_User = user.Id,
                GhiChu = "Hóa đơn test",
                TrangThai = 1 // Trạng thái ban đầu là 1 (đang xử lý)
            };
            _context.AppUsers.Add(user);
            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync();

            // Act - Gọi API để hủy hóa đơn
            var result = await _controller.HuyDon(hoaDon.Id);

            // Assert - Kiểm tra kết quả
            var jsonResult = Assert.IsType<JsonResult>(result);
            var updatedHoaDon = Assert.IsType<HoaDon>(jsonResult.Value);
            Assert.Equal(2, updatedHoaDon.TrangThai); // Kiểm tra trạng thái đã được cập nhật thành 2 (hủy)

            // Kiểm tra trong database
            var hoaDonInDb = await _context.HoaDons.FindAsync(hoaDon.Id);
            Assert.Equal(2, hoaDonInDb.TrangThai);
        }

        // ChiTietHD05: Hủy hóa đơn khi Id không tồn tại
        [Fact]
        public async Task ChiTietHD05_HuyDon_ReturnsNull_WhenIdDoesNotExist()
        {

            // Act - Gọi API
            var result = await _controller.HuyDon(999);

            // Assert - Kiểm tra kết quả
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Null(jsonResult.Value); // Kiểm tra kết quả là null
        }

        // ChiTietHD06: Hủy hóa đơn khi hóa đơn đã bị hủy trước đó
        [Fact]
        public async Task ChiTietHD06_HuyDon_Success_WhenHoaDonAlreadyCancelled()
        {
            // Arrange - Chuẩn bị dữ liệu
            var user = new AppUser { FirstName = "John", LastName = "Doe" };
            var hoaDon = new HoaDon
            {
                Id_User = user.Id,
                GhiChu = "Hóa đơn test",
                TrangThai = 2 // Trạng thái ban đầu đã là 2 (hủy)
            };
            _context.AppUsers.Add(user);
            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync();

            // Act - Gọi API
            var result = await _controller.HuyDon(hoaDon.Id);

            // Assert - Kiểm tra kết quả
            var jsonResult = Assert.IsType<JsonResult>(result);
            var updatedHoaDon = Assert.IsType<HoaDon>(jsonResult.Value);
            Assert.Equal(2, updatedHoaDon.TrangThai); // Kiểm tra trạng thái vẫn là 2
        }
        // ChiTietHD08: Lấy thông tin tài khoản khi IdUser tồn tại
        [Fact]
        public async Task ChiTietHD08_ThongTinTaiKhoan_ReturnsUserInfo_WhenIdUserExists()
        {
            // Arrange - Chuẩn bị dữ liệu
            var user = new AppUser
            {
                Id = "user123",
                FirstName = "Nguyễn",
                LastName = "Văn A",
                DiaChi = "123 Đường Láng, Hà Nội",
                SDT = "0123456789"
            };
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            // Act - Gọi API
            var result = await _controller.ThongTinTaiKhoan("user123");

            // Assert - Kiểm tra kết quả
            var jsonResult = Assert.IsType<JsonResult>(result);
            var userInfo = Assert.IsType<ThongTinTaiKhoan>(jsonResult.Value);
            Assert.Equal("Nguyễn", userInfo.Ho);
            Assert.Equal("Văn A", userInfo.Ten);
            Assert.Equal("123 Đường Láng, Hà Nội", userInfo.DiaChi);
            Assert.Equal("0123456789", userInfo.SoDienThoai);
        }

        // ChiTietHD09: Lấy thông tin tài khoản khi IdUser không tồn tại
        [Fact]
        public async Task ChiTietHD09_ThongTinTaiKhoan_ReturnsNull_WhenIdUserDoesNotExist()
        {

            // Act - Gọi API
            var result = await _controller.ThongTinTaiKhoan("user999");

            // Assert - Kiểm tra kết quả
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Null(jsonResult.Value); // Kiểm tra kết quả là null
        }

        // ChiTietHD10: Lấy thông tin tài khoản khi IdUser là chuỗi rỗng
        [Fact]
        public async Task ChiTietHD10_ThongTinTaiKhoan_ReturnsNull_WhenIdUserIsEmpty()
        {
            // Act - Gọi API
            var result = await _controller.ThongTinTaiKhoan("");

            // Assert - Kiểm tra kết quả
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Null(jsonResult.Value); // Kiểm tra kết quả là null
        }
    }

}
