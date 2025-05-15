using API.Controllers;
using API.Data;
using API.Models;
using Microsoft.AspNetCore.Mvc;
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
        // LichSuMH01: Lấy lịch sử mua hàng khi IdUser tồn tại và có hóa đơn
        [Fact]
        public async Task LichSuMH01_GetLichSuMuaHang_ReturnsList_WhenIdUserExistsAndHasHoaDons()
        {
            // Arrange - Chuẩn bị dữ liệu
            var user = new AppUser { FirstName = "John", LastName = "Doe" };
            var hoaDon1 = new HoaDon
            {
                Id_User = user.Id,
                GhiChu = "Hóa đơn 1",
                Tinh = "Hà Nội",
                Huyen = "Ba Đình",
                Xa = "Phúc Xá",
                DiaChi = "123 Đường Láng",
                TongTien = 500000,
                TrangThai = 1
            };
            var hoaDon2 = new HoaDon
            {
                Id_User = user.Id,
                GhiChu = "Hóa đơn 2",
                Tinh = "Hà Nội",
                Huyen = "Cầu Giấy",
                Xa = "Nghĩa Đô",
                DiaChi = "456 Đường Cầu Giấy",
                TongTien = 300000,
                TrangThai = 2
            };
            _context.AppUsers.Add(user);
            _context.HoaDons.AddRange(hoaDon1, hoaDon2);
            await _context.SaveChangesAsync();

            // Act - Gọi API với IdUser = "user123"
            var result = await _controller.GetLichSuMuaHang(user.Id);

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<HoaDon>>>(result);
            var data = Assert.IsAssignableFrom<IEnumerable<HoaDon>>(actionResult.Value);
            Assert.NotEmpty(data); // Kiểm tra danh sách không rỗng
            Assert.Contains(data, h => h.GhiChu == "Hóa đơn 1" && h.TongTien == 500000);
            Assert.Contains(data, h => h.GhiChu == "Hóa đơn 2" && h.TongTien == 300000);
        }        
    }
}
