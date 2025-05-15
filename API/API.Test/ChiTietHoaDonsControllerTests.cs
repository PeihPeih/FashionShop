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

        // ChiTietHD03: Hủy hóa đơn khi Id tồn tại
        [Fact]
        public async Task ChiTietHD03_HuyDon_Success_WhenIdExists()
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

        // ChiTietHD04: Lấy thông tin tài khoản khi IdUser tồn tại
        [Fact]
        public async Task ChiTietHD04_ThongTinTaiKhoan_ReturnsUserInfo_WhenIdUserExists()
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
    }

}
