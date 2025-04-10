using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helper.SignalR;
using API.Models;
using Xunit;
using API.Test;

namespace API.Test
{
    public class MaGiamGiasControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly MaGiamGiasController _controller;

        public MaGiamGiasControllerTests() : base()
        {
            _context = new DPContext(_options);

            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            _controller = new MaGiamGiasController(_context, _hubContext);
        }

        // MGG01: Kiểm tra lấy danh sách mã giảm giá thành công khi có dữ liệu trong DB
        [Fact]
        public async Task GetMaGiamGias_ShouldReturnAll_WhenHaveData()
        {

            // Arrange
            var expected1 = new MaGiamGia { Code = "X1", SoTienGiam = 10000 };
            var expected2 = new MaGiamGia { Code = "X2", SoTienGiam = 20000 };
            _context.MaGiamGias.AddRange(expected1, expected2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetMaGiamGias();

            // Assert
            var list = Assert.IsAssignableFrom<IEnumerable<MaGiamGia>>(result.Value);
            var actual = list.ToList();
            Assert.Contains(actual, x => x.Code == "X1" && x.SoTienGiam == 10000);
            Assert.Contains(actual, x => x.Code == "X2" && x.SoTienGiam == 20000);
        }

        // MGG02: Kiểm tra trả về danh sách rỗng khi không có mã giảm giá nào
        [Fact]
        public async Task GetMaGiamGias_ShouldReturnEmpty_WhenNoData()
        {

            // Arrange
            _context.MaGiamGias.RemoveRange(_context.MaGiamGias); // Clear DB
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetMaGiamGias();

            // Assert
            var list = Assert.IsAssignableFrom<IEnumerable<MaGiamGia>>(result.Value);
            Assert.Empty(list);
        }

        // MGG03: Kiểm tra tạo mã giảm giá thành công với đầu vào hợp lệ
        [Fact]
        public async Task TaoMaGiamGia_ShouldCreateSuccessfully_WhenValidInput()
        {

            // Arrange
            var input = new UploadMaGiamGia { SoTienGiam = 30000 };

            // Act
            var result = await _controller.TaoMaGiamGia(input);

            // Assert
            var ok = Assert.IsType<OkResult>(result);
            Assert.Equal(200, ok.StatusCode);

            var added = _context.MaGiamGias
                .OrderByDescending(x => x.Id) // Sắp xếp theo ID trước khi lấy
                .First();

            Assert.Equal(30000, added.SoTienGiam);
            Assert.False(string.IsNullOrEmpty(added.Code));

        }

        // MGG04: Kiểm tra cập nhật mã giảm giá khi ID hợp lệ
        [Fact]
        public async Task SuaMaGiamGia_ShouldUpdateSuccessfully_WhenIdIsValid()
        {

            // Arrange
            var existing = new MaGiamGia { Code = "ABC", SoTienGiam = 5000 };
            _context.MaGiamGias.Add(existing);
            await _context.SaveChangesAsync();

            var input = new UploadMaGiamGia { SoTienGiam = 9999 };

            // Act
            var result = await _controller.SuaMaGiamGia(input, existing.Id);

            // Assert
            var ok = Assert.IsType<OkResult>(result);
            var updated = await _context.MaGiamGias.FindAsync(existing.Id);
            Assert.Equal(9999, updated.SoTienGiam);
            Assert.Equal(5, updated.Code.Length);
        }

        //  MGG05: Kiểm tra xóa mã giảm giá thành công khi ID tồn tại
        [Fact]
        public async Task DeleteMaGiamGias_ShouldReturnOk_WhenIdExists()
        {

            // Arrange
            var entity = new MaGiamGia { Code = "DEL", SoTienGiam = 8000 };
            _context.MaGiamGias.Add(entity);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.DeleteMaGiamGias(entity.Id);

            // Assert
            var ok = Assert.IsType<OkResult>(result);
            var deleted = await _context.MaGiamGias.FindAsync(entity.Id);
            Assert.Null(deleted);
        }

        // MGG06: Kiểm tra xóa mã giảm giá trả về NotFound khi ID không tồn tại
        [Fact]
        public async Task DeleteMaGiamGias_ShouldReturnNotFound_WhenIdInvalid()
        {

            // Arrange
            var invalidId = 999;

            // Act
            var result = await _controller.DeleteMaGiamGias(invalidId);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var error = notFound.Value;
            var messageProperty = error.GetType().GetProperty("message");
            var messageValue = messageProperty?.GetValue(error)?.ToString();
            Assert.Equal("Không tìm thấy mã giảm giá với ID 999", messageValue);
        }
    }
}
