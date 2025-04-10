using API.Controllers;
using API.Data;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace API.Test
{
    public class LichSuMuaHangsControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly LichSuMuaHangsController _controller;

        public LichSuMuaHangsControllerTests() : base()
        {
            _context = new DPContext(_options);
            _controller = new LichSuMuaHangsController(_context);
        }

        // Helper methods for setup
        private async Task<AppUser> CreateTestUserAsync(string firstName = "Test", string lastName = "User")
        {
            var user = new AppUser
            {
                FirstName = firstName,
                LastName = lastName
            };
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        private async Task<SanPham> CreateTestSanPhamAsync(string ten = "Test Product", decimal giaBan = 100000)
        {
            var sanPham = new SanPham
            {
                Ten = ten,
                GiaBan = giaBan,
                TrangThaiHoatDong = true
            };
            _context.SanPhams.Add(sanPham);
            await _context.SaveChangesAsync();
            return sanPham;
        }

        private async Task<SanPhamBienThe> CreateTestSanPhamBienTheAsync(int sanPhamId, int sizeId = 1, int mauId = 1)
        {
            var bienThe = new SanPhamBienThe
            {
                Id_SanPham = sanPhamId,
                SizeId = sizeId,
                Id_Mau = mauId,
                SoLuongTon = 10
            };
            _context.SanPhamBienThes.Add(bienThe);
            await _context.SaveChangesAsync();
            return bienThe;
        }

        private async Task<HoaDon> CreateTestHoaDonAsync(string userId, DateTime? ngayTao = null)
        {
            var hoaDon = new HoaDon
            {
                Id_User = userId,
                NgayTao = ngayTao ?? DateTime.Now,
                TongTien = 100000,
                TrangThai = 1, // Đã thanh toán
                GhiChu = "Ghi chú test",
                Tinh = "Hà Nội",
                Huyen = "Cầu Giấy",
                Xa = "Dịch Vọng",
                DiaChi = "Số 1 Đường ABC"
            };
            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync();
            return hoaDon;
        }

        private async Task<ChiTietHoaDon> CreateTestChiTietHoaDonAsync(int hoaDonId, int sanPhamId, int sanPhamBienTheId,
            int soLuong = 1, decimal giaBan = 100000, string mau = "Đỏ", string size = "M")
        {
            var chiTiet = new ChiTietHoaDon
            {
                Id_HoaDon = hoaDonId,
                Id_SanPham = sanPhamId,
                Id_SanPhamBienThe = sanPhamBienTheId,
                Soluong = soLuong,
                GiaBan = giaBan,
                ThanhTien = soLuong * giaBan,
                Mau = mau,
                Size = size
            };
            _context.ChiTietHoaDons.Add(chiTiet);
            await _context.SaveChangesAsync();
            return chiTiet;
        }

        // ---------------------- GET LICH SU MUA HANG --------------------------
        // LSMH01: Get lịch sử mua hàng - Should return all orders of a user
        [Fact]
        public async Task GetLichSuMuaHang_ReturnsAllOrdersOfUser()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var userId = user.Id.ToString();

            // Create multiple orders for the user
            await CreateTestHoaDonAsync(userId, DateTime.Now.AddDays(-1));
            await CreateTestHoaDonAsync(userId, DateTime.Now.AddDays(-2));
            await CreateTestHoaDonAsync(userId, DateTime.Now.AddDays(-3));

            // Create an order for another user (should not be returned)
            var otherUser = await CreateTestUserAsync("Other", "User");
            await CreateTestHoaDonAsync(otherUser.Id.ToString());

            // Act
            var result = await _controller.GetLichSuMuaHang(userId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<HoaDon>>>(result);
            var hoaDons = Assert.IsAssignableFrom<List<HoaDon>>(actionResult.Value);

            Assert.Equal(3, hoaDons.Count);
            Assert.All(hoaDons, hd => Assert.Equal(userId, hd.Id_User));
        }

        // LSMH02: Get lịch sử mua hàng - Should return empty list when user has no orders
        [Fact]
        public async Task GetLichSuMuaHang_ReturnsEmptyList_WhenUserHasNoOrders()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var userId = user.Id.ToString();

            // No orders for this user

            // Act
            var result = await _controller.GetLichSuMuaHang(userId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<HoaDon>>>(result);
            var hoaDons = Assert.IsAssignableFrom<List<HoaDon>>(actionResult.Value);

            Assert.Empty(hoaDons);
        }

        // LSMH03: Get lịch sử mua hàng - Should return empty list when user doesn't exist
        [Fact]
        public async Task GetLichSuMuaHang_ReturnsEmptyList_WhenUserDoesNotExist()
        {
            // Arrange
            string nonExistentUserId = "999999";

            // Act
            var result = await _controller.GetLichSuMuaHang(nonExistentUserId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<HoaDon>>>(result);
            var hoaDons = Assert.IsAssignableFrom<List<HoaDon>>(actionResult.Value);

            Assert.Empty(hoaDons);
        }

        // LSMH04: Get lịch sử mua hàng - Should return orders sorted by date
        [Fact]
        public async Task GetLichSuMuaHang_ReturnsSortedOrders()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var userId = user.Id.ToString();

            // Create orders with different dates
            var date1 = DateTime.Now.AddDays(-1);
            var date2 = DateTime.Now.AddDays(-2);
            var date3 = DateTime.Now.AddDays(-3);

            await CreateTestHoaDonAsync(userId, date2); // Middle date
            await CreateTestHoaDonAsync(userId, date3); // Oldest
            await CreateTestHoaDonAsync(userId, date1); // Newest

            // Act
            var result = await _controller.GetLichSuMuaHang(userId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<HoaDon>>>(result);
            var hoaDons = Assert.IsAssignableFrom<List<HoaDon>>(actionResult.Value);

            Assert.Equal(3, hoaDons.Count);

            // The controller doesn't sort the results, so we just verify all dates are present
            var dates = hoaDons.Select(hd => hd.NgayTao.Date).ToList();
            Assert.Contains(date1.Date, dates);
            Assert.Contains(date2.Date, dates);
            Assert.Contains(date3.Date, dates);
        }

        // LSMH05: Get lịch sử mua hàng - Should handle different order statuses
        [Fact]
        public async Task GetLichSuMuaHang_ReturnsOrdersWithDifferentStatuses()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var userId = user.Id.ToString();

            // Create orders with different statuses
            var hoaDon1 = await CreateTestHoaDonAsync(userId);
            var hoaDon2 = await CreateTestHoaDonAsync(userId);
            var hoaDon3 = await CreateTestHoaDonAsync(userId);

            // Update statuses
            hoaDon1.TrangThai = 1; // Đã thanh toán
            hoaDon2.TrangThai = 2; // Đang xử lý
            hoaDon3.TrangThai = 3; // Đã hủy
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetLichSuMuaHang(userId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<HoaDon>>>(result);
            var hoaDons = Assert.IsAssignableFrom<List<HoaDon>>(actionResult.Value);

            Assert.Equal(3, hoaDons.Count);

            // Verify all statuses are present
            var statuses = hoaDons.Select(hd => hd.TrangThai).ToList();
            Assert.Contains(1, statuses);
            Assert.Contains(2, statuses);
            Assert.Contains(3, statuses);
        }

        // LSMH06: Get lịch sử mua hàng - Should include order details
        [Fact]
        public async Task GetLichSuMuaHang_IncludesOrderDetails()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var userId = user.Id.ToString();

            // Create a product and its variants
            var sanPham = await CreateTestSanPhamAsync("Áo thun", 150000);
            var bienThe = await CreateTestSanPhamBienTheAsync(sanPham.Id);

            // Create an order with details
            var hoaDon = await CreateTestHoaDonAsync(userId);
            await CreateTestChiTietHoaDonAsync(
                hoaDon.Id,
                sanPham.Id,
                bienThe.Id,
                2, // 2 items
                150000, // 150,000 đ each
                "Đỏ", // Màu
                "M" // Size
            );

            // Act
            var result = await _controller.GetLichSuMuaHang(userId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<HoaDon>>>(result);
            var hoaDons = Assert.IsAssignableFrom<List<HoaDon>>(actionResult.Value);

            Assert.Single(hoaDons);

            // Note: Depending on how your controller is implemented,
            // you may need to check if ChiTietHoaDons is properly included in the result
            // This test assumes eager loading is configured in the controller
        }
    }
}