using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helper.SignalR;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Xunit;
using System.Linq;

namespace API.Test
{
    public class NhaCungCapsControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly NhaCungCapsController _controller;

        public NhaCungCapsControllerTests() : base()
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
            _controller = new NhaCungCapsController(_context, _hubContext);
        }

        // NhaCungCap01: Lấy danh sách nhà cung cấp khi có dữ liệu
        [Fact]
        public async Task NhaCungCap01_GetAllNhaCungCap_ReturnsList_WhenDataExists()
        {
            // Arrange - Chuẩn bị dữ liệu
            var nhaCungCap1 = new NhaCungCap
            {
                Ten = "Nhà cung cấp A",
                SDT = "0123456789",
                ThongTin = "Thông tin A",
                DiaChi = "123 Đường A, Hà Nội"
            };
            var nhaCungCap2 = new NhaCungCap
            {
                Ten = "Nhà cung cấp B",
                SDT = "0987654321",
                ThongTin = "Thông tin B",
                DiaChi = "456 Đường B, Hà Nội"
            };
            _context.NhaCungCaps.AddRange(nhaCungCap1, nhaCungCap2);
            await _context.SaveChangesAsync();

            // Act - Gọi API
            var result = await _controller.GetAllNhaCungCap();

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NhaCungCap>>>(result);
            var data = Assert.IsAssignableFrom<IEnumerable<NhaCungCap>>(actionResult.Value);
            Assert.NotEmpty(data); // Kiểm tra danh sách không rỗng
            Assert.Contains(data, n => n.Ten == "Nhà cung cấp A");
            Assert.Contains(data, n => n.Ten == "Nhà cung cấp B");
        }

        // NhaCungCap02: Thêm nhà cung cấp với dữ liệu hợp lệ
        [Fact]
        public async Task NhaCungCap02_PostNhaCungCap_Success_WhenDataIsValid()
        {
            // Arrange - Chuẩn bị dữ liệu
            var upload = new UploadNhaCungCap
            {
                Ten = "Nhà cung cấp C",
                SDT = "0123456789",
                ThongTin = "Thông tin C",
                DiaChi = "789 Đường C, Hà Nội"
            };

            // Act - Gọi API để thêm nhà cung cấp
            var result = await _controller.PostNhaCungCapAsync(upload);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<OkResult>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var nhaCungCap = await _context.NhaCungCaps.FirstOrDefaultAsync(n => n.Ten == "Nhà cung cấp C");
            Assert.NotNull(nhaCungCap); // Kiểm tra nhà cung cấp đã được thêm
            Assert.Equal("0123456789", nhaCungCap.SDT);
            Assert.Equal("Thông tin C", nhaCungCap.ThongTin);
            Assert.Equal("789 Đường C, Hà Nội", nhaCungCap.DiaChi);
        }

        // NhaCungCap03: Cập nhật nhà cung cấp khi Id tồn tại
        [Fact]
        public async Task NhaCungCap03_PutNhaCungCap_Success_WhenIdExists()
        {
            // Arrange - Chuẩn bị dữ liệu
            var nhaCungCap = new NhaCungCap
            {
                Ten = "Nhà cung cấp D",
                SDT = "0123456789",
                ThongTin = "Thông tin D",
                DiaChi = "123 Đường D, Hà Nội"
            };
            _context.NhaCungCaps.Add(nhaCungCap);
            await _context.SaveChangesAsync();

            var upload = new UploadNhaCungCap
            {
                Ten = "Nhà cung cấp D Updated",
                SDT = "0987654321",
                ThongTin = "Thông tin D Updated",
                DiaChi = "456 Đường D Updated, Hà Nội"
            };

            // Act - Gọi API để cập nhật
            var result = await _controller.PutNhaCungCapAsync(upload, nhaCungCap.Id);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<OkResult>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var updatedNhaCungCap = await _context.NhaCungCaps.FindAsync(nhaCungCap.Id);
            Assert.Equal("Nhà cung cấp D Updated", updatedNhaCungCap.Ten);
            Assert.Equal("0987654321", updatedNhaCungCap.SDT);
            Assert.Equal("Thông tin D Updated", updatedNhaCungCap.ThongTin);
            Assert.Equal("456 Đường D Updated, Hà Nội", updatedNhaCungCap.DiaChi);
        }

        // NhaCungCap04: Xóa nhà cung cấp khi Id tồn tại và không có sản phẩm liên quan
        [Fact]
        public async Task NhaCungCap04_DeleteNhaCungCap_Success_WhenIdExistsAndNoRelatedProducts()
        {
            // Arrange - Chuẩn bị dữ liệu
            var nhaCungCap = new NhaCungCap
            {
                Ten = "Nhà cung cấp F",
                SDT = "0123456789",
                ThongTin = "Thông tin F",
                DiaChi = "123 Đường F, Hà Nội"
            };
            _context.NhaCungCaps.Add(nhaCungCap);
            await _context.SaveChangesAsync();

            // Act - Gọi API để xóa
            var result = await _controller.DeleteNhaCungCapAsync(nhaCungCap.Id);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<OkResult>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var deletedNhaCungCap = await _context.NhaCungCaps.FindAsync(nhaCungCap.Id);
            Assert.Null(deletedNhaCungCap); // Kiểm tra nhà cung cấp đã bị xóa
        }
    }
}
