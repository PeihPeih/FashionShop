﻿using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using API.Controllers;
using API.Data;
using API.Models;
using Microsoft.AspNetCore.SignalR;
using API.Helper.SignalR;
using Newtonsoft.Json.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using API.Dtos;
using System.Threading.Tasks;
using System;
using API.Test;

namespace API.Test {
    public class SizesControllerTests : TestBase {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly SizesController _controller;

        public SizesControllerTests() : base() {
            _context = new DPContext(_options);

            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            _controller = new SizesController(_context, _hubContext);
        }

        // Size01: Kiểm tra trả về danh sách kích thước khi có dữ liệu hợp lệ
        [Fact]
        public void GetListSizeTheoMau_ValidData_ReturnsSizeList() {
            // Arrange
            JObject testJson = new JObject {
                { "id_san_pham", 1 },
                { "mamau", "Đen" },
            };

            // Act
            var result = _controller.getListSizeTheoMau(testJson) as JsonResult;

            // Assert
            Assert.NotNull(result);

            var jsonList = result.Value as IEnumerable<object>;
            Assert.NotNull(jsonList);

            var sizeList = jsonList
                .Select(item => item?.GetType().GetProperty("size")?.GetValue(item)?.ToString())
                .Where(size => size != null)
                .ToList();

            var expected = new List<string> { "S", "M", "L" };
            Assert.Equal(expected, sizeList);
            Assert.All(sizeList, s => Assert.False(string.IsNullOrWhiteSpace(s)));
        }

        // Size02: Kiểm tra trả về mã lỗi 500 và thông báo lỗi khi đầu vào không hợp lệ
        [Fact]
        public void GetListSizeTheoMau_InvalidInput_Returns500WithError() {
            // Arrange
            // Đầu vào không hợp lệ: id_san_pham không phải số
            var json = new JObject
            {
                { "id_san_pham", "invalid" }, // Gây ra FormatException
                { "mamau", "RED" }
            };

            // Act
            var result = _controller.getListSizeTheoMau(json) as ObjectResult;

            // Assert
            Assert.Equal(500, result.StatusCode);
            var error = result.Value as dynamic;
            Assert.NotNull(error);
            Assert.Equal("Id sản phẩm không hợp lệ", error.error.ToString());
        }

        // Size03: Kiểm tra trả về danh sách kích thước loại
        [Fact]
        public async Task GetSizes_ReturnsSizeLoaiList() {
            // Arrange

            // Act
            var result = await _controller.GetSizes();

            // Assert
            var okResult = Assert.IsType<ActionResult<IEnumerable<SizeLoai>>>(result);
            var sizeLoaiList = Assert.IsType<List<SizeLoai>>(okResult.Value);
            Assert.NotEmpty(sizeLoaiList); 
            var firstItem = sizeLoaiList.First();
            Assert.Equal(1, firstItem.Id); 
            Assert.Equal(1, firstItem.Id_Loai);
            Assert.Equal("Áo", firstItem.TenLoai);
        }

        // Size04: Kiểm tra trả về danh sách tên kích thước loại khi có dữ liệu

        [Fact]
        public async Task GetTenSizeLoais_ReturnsList_WhenDataExists() {
            // Arrange
            var loai = new Loai { Ten = "Shirt" };
            _context.Loais.Add(loai);
            await _context.SaveChangesAsync();

            _context.Sizes.AddRange(
                new Size { TenSize = "Mini", Id_Loai = loai.Id },
                new Size { TenSize = "Super", Id_Loai = loai.Id }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetTenSizeLoais();

            // Assert
            var okResult = Assert.IsType<ActionResult<IEnumerable<TenSizeLoai>>>(result);
            var value = Assert.IsAssignableFrom<IEnumerable<TenSizeLoai>>(okResult.Value);
            var items = value.ToList();

            Assert.NotEmpty(items);
            Assert.Contains(items, item => item.SizeLoaiTen == "Mini Shirt");
            Assert.Contains(items, item => item.SizeLoaiTen == "Super Shirt");
        }

        // Size05: Kiểm tra trả về thông tin kích thước khi ID hợp lệ
        [Fact]
        public async Task GetSizeByID_ValidID_ReturnSize() {
            // Arrange
            int id = 99;
            _context.Sizes.Add(new Size { Id = id, TenSize = "Siêu to khổng lồ", Id_Loai = 1 });
            // Act
            var result = await _controller.GetSize(id);

            // Assert
            var okResult = Assert.IsType<ActionResult<Size>>(result);
            var value = Assert.IsType<Size>(okResult.Value);

            Assert.NotNull(value);
            Assert.Equal(99, value.Id);
            Assert.Equal("Siêu to khổng lồ", value.TenSize);
        }

        // Size06: Kiểm tra trả về NotFound khi ID không tồn tại
        [Fact]
        public async Task GetSizeByID_InvalidID_ReturnNotFound() {
            // Arrange
            int id = 99999;

            // Act
            var result = await _controller.GetSize(id);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        // Size07: Kiểm tra thêm kích thước hợp lệ và trả về CreatedAtAction
        [Fact]
        public async Task PostSize_ValidUpload_ReturnsCreatedAtAction() {
            // Arrange
            var upload = new UploadSize {
                TenSize = "Medium",
                Id_Loai = 1
            };

            // Act
            var result = await _controller.PostSize(upload);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal("GetSize", createdAtActionResult.ActionName);
            Assert.Equal(201, createdAtActionResult.StatusCode);

            var returnedSize = Assert.IsType<Size>(createdAtActionResult.Value);
            Assert.Equal(upload.TenSize, returnedSize.TenSize);
            Assert.Equal(upload.Id_Loai, returnedSize.Id_Loai);

            // Kiểm tra dữ liệu trong DB
            var sizeInDb = await _context.Sizes.FirstOrDefaultAsync(s => s.TenSize == upload.TenSize && s.Id_Loai == upload.Id_Loai);
            Assert.NotNull(sizeInDb);
            Assert.Equal(returnedSize.Id, sizeInDb.Id);

            var notificationInDb = await _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == upload.TenSize && n.TranType == "Add");
            Assert.NotNull(notificationInDb);
        }

        // Size08: Kiểm tra cập nhật kích thước thành công khi ID tồn tại
        [Fact]
        public async Task PutSize_UpdatesSizeAndReturnsNoContent_WhenSizeExists() {
            // Arrange
            var size = new Size { TenSize = "TestSize", Id_Loai = 1 };
            _context.Sizes.Add(size);
            await _context.SaveChangesAsync();

            int sizeId = size.Id;
            var upload = new UploadSize {
                TenSize = "Giant",
                Id_Loai = 2
            };
            // Act
            var result = await _controller.PutSize(sizeId, upload);

            // Assert
            Assert.IsType<NoContentResult>(result);

            // Kiểm tra xem dữ liệu trong DB đã được cập nhật chưa
            var updatedSize = await _context.Sizes.FindAsync(sizeId);
            Assert.NotNull(updatedSize);
            Assert.Equal(upload.TenSize, updatedSize.TenSize);
            Assert.Equal(upload.Id_Loai, updatedSize.Id_Loai);

            // Kiểm tra notification đã được thêm
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == upload.TenSize && n.TranType == "Edit");
            Assert.NotNull(notification);
        }

        // Size09: Kiểm tra xóa kích thước thành công khi ID tồn tại 
        [Fact]
        public async Task DeleteSize_ExistingId_ReturnsNoContent() {
            // Arrange
            var size = new Size { TenSize = "Large", Id_Loai = 1 };
            _context.Sizes.Add(size);
            await _context.SaveChangesAsync();
            int sizeId = size.Id;

            // Act
            var result = await _controller.DeleteSize(sizeId);

            // Assert
            Assert.IsType<NoContentResult>(result);

            // Kiểm tra Size đã bị xóa
            var sizeInDb = await _context.Sizes.FindAsync(sizeId);
            Assert.Null(sizeInDb);

            // Kiểm tra Notification đã được thêm
            var notificationInDb = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == size.TenSize && n.TranType == "Delete");
            Assert.NotNull(notificationInDb);
        }

        // Size10: Kiểm tra trả về NotFound khi xóa kích thước với ID không tồn tại
        [Fact]
        public async Task DeleteSize_NonExistingId_ReturnsNotFound() {
            // Arrange
            int nonExistentId = 999; // Id không tồn tại

            // Act
            var result = await _controller.DeleteSize(nonExistentId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
            Assert.Equal(404, ((NotFoundResult)result).StatusCode);
        }
    }
}
