using Xunit;
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

namespace API.Tests {
    public class SizesControllerTests {
        private DPContext GetDbContextWithData() {
            var options = new DbContextOptionsBuilder<DPContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new DPContext(options);

            // Seed Loai
            var loai = new Loai { Id = 1, Ten = "Áo" };
            context.Loais.Add(loai);

            // Seed SanPham
            var sp = new SanPham { Id = 1, Id_Loai = 1 };
            context.SanPhams.Add(sp);

            // Seed MauSac
            var mau = new MauSac { Id = 1, MaMau = "Red", Id_Loai = 1 };
            context.MauSacs.Add(mau);

            // Seed SanPhamBienThe
            var bienThe = new SanPhamBienThe { Id_Mau = 1, Id_SanPham = 1, SizeId = 1 };
            context.SanPhamBienThes.Add(bienThe);

            // Seed Sizes
            var size = new Size { Id = 1, TenSize = "M", Id_Loai = 1 };
            context.Sizes.Add(size);

            context.SaveChanges();
            return context;
        }

        // Size01: Kiểm tra phương thức trả về list size theo mẫu sản phẩm
        [Fact]
        public void GetListSizeTheoMau_ReturnsSizeList() {
            // Arrange
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var context = GetDbContextWithData();
            var controller = new SizesController(context, mockHub.Object);

            var json = new JObject {
                ["id_san_pham"] = 1,
                ["mamau"] = "Red"
            };

            // Act
            var result = controller.getListSizeTheoMau(json) as JsonResult;

            // Assert
            Assert.NotNull(result);
            var list = result.Value as IEnumerable<object>;
            Assert.NotNull(list);
            Assert.NotEmpty(list);
        }

        // Size02: Kiểm tra phương thức trả về danh sách tất cả size
        [Fact]
        public async Task GetSizes_ReturnsExpectedList() {
            // Arrange
            var context = GetDbContextWithData(); 
            var hubContext = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var controller = new SizesController(context, hubContext.Object);

            // Act
            var result = await controller.GetSizes();

            // Assert
            var okResult = Assert.IsType<ActionResult<IEnumerable<SizeLoai>>>(result);
            var value = Assert.IsType<List<SizeLoai>>(okResult.Value);

            Assert.Single(value); 
            Assert.Equal(1, value[0].Id); 
            Assert.Equal(1, value[0].Id_Loai); 
            Assert.Equal("Áo", value[0].TenLoai); 
            Assert.Equal("M", value[0].TenSize); 
        }

        // Size03:  Kiểm tra phương thức trả về danh sách tên của các size
        [Fact]
        public async Task GetTenSizeLoais_ReturnsExpectedList() {
            // Arrange
            var context = GetDbContextWithData();
            var hubContext = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var controller = new SizesController(context, hubContext.Object);

            // Act
            var result = await controller.GetTenSizeLoais();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<TenSizeLoai>>>(result);
            var value = Assert.IsType<List<TenSizeLoai>>(actionResult.Value);

            Assert.Single(value); 
            Assert.Equal(1, value[0].Id); 
            Assert.Equal("M Áo", value[0].SizeLoaiTen); 
        }

        // Size04: Kiểm tra phương thức trả về size khi ID hợp lệ
        [Fact]
        public async Task GetSize_ReturnsSize_WhenIdExists() {
            // Arrange
            var context = GetDbContextWithData(); 
            var hubContext = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var controller = new SizesController(context, hubContext.Object);

            // Act
            var result = await controller.GetSize(1); 

            // Assert
            var actionResult = Assert.IsType<ActionResult<Size>>(result);
            var size = Assert.IsType<Size>(actionResult.Value);
            Assert.Equal(1, size.Id);
            Assert.Equal("M", size.TenSize);
        }

        // Size05: Kiểm tra phương thức trả về NotFound khi ID không hợp lệ
        [Fact]
        public async Task GetSize_ReturnsNotFound_WhenIdDoesNotExist() {
            // Arrange
            var context = GetDbContextWithData();
            var hubContext = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var controller = new SizesController(context, hubContext.Object);

            // Act
            var result = await controller.GetSize(99); 

            // Assert
            var actionResult = Assert.IsType<ActionResult<Size>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        // Size06: Kiểm tra phương thức cập nhật size khi ID hợp lệ
        [Fact]
        public async Task PutSize_UpdatesSize_WhenIdExists() {
            // Arrange
            var context = GetDbContextWithData();
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();

            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

            var controller = new SizesController(context, mockHub.Object);
            var upload = new UploadSize { TenSize = "L", Id_Loai = 1};

            // Act
            var result = await controller.PutSize(1, upload);

            // Assert
            Assert.IsType<NoContentResult>(result);

            var updatedSize = await context.Sizes.FindAsync(1);
            Assert.Equal("L", updatedSize.TenSize);

            var noti = context.Notifications.FirstOrDefault();
            Assert.NotNull(noti);
            Assert.Equal("Edit", noti.TranType);
            Assert.Equal("L", noti.TenSanPham);
        }

        // Size07: Kiểm tra phương thức cập nhật size khi ID không hợp lệ
        [Fact]
        public async Task PutSize_ReturnsException_WhenIdNotFound() {
            // Arrange
            var context = GetDbContextWithData();
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();

            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

            var controller = new SizesController(context, mockHub.Object);
            var upload = new UploadSize { TenSize = "XL", Id_Loai = 1 };

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(() => controller.PutSize(99, upload));
        }

        // Size08: Kiểm tra phương thức tạo mới size khi dữ liệu hợp lệ
        [Fact]
        public async Task PostSize_ReturnsCreatedAtActionResult_WhenDataIsValid() {
            // Arrange
            var context = GetDbContextWithData();
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();

            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

            var controller = new SizesController(context, mockHub.Object);
            var upload = new UploadSize { TenSize = "M", Id_Loai = 1 };

            // Act
            var result = await controller.PostSize(upload);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var createdSize = Assert.IsType<Size>(createdAtActionResult.Value);
            Assert.Equal(upload.TenSize, createdSize.TenSize);
            Assert.Equal(upload.Id_Loai, createdSize.Id_Loai);

            var sizeInDb = await context.Sizes.FindAsync(createdSize.Id);
            Assert.NotNull(sizeInDb);

            var notification = context.Notifications.FirstOrDefault();
            Assert.NotNull(notification);
            Assert.Equal("Add", notification.TranType);
            Assert.Equal(upload.TenSize, notification.TenSanPham);
        }

        // Size09: Kiểm tra phương thức xóa size khi ID hợp lệ
        [Fact]
        public async Task DeleteSize_ReturnsNoContent_WhenIdExists() {
            // Arrange
            var context = GetDbContextWithData();
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();

            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

            var controller = new SizesController(context, mockHub.Object);

            // Act
            var result = await controller.DeleteSize(1); 

            // Assert
            Assert.IsType<NoContentResult>(result);

            var sizeInDb = await context.Sizes.FindAsync(1);
            Assert.Null(sizeInDb); 

            var notification = context.Notifications.FirstOrDefault();
            Assert.NotNull(notification);
            Assert.Equal("Delete", notification.TranType);

            Assert.Equal("M", notification.TenSanPham);
        }

        // Size10: Kiểm tra phương thức xóa size khi ID không hợp lệ
        [Fact]
        public async Task DeleteSize_ReturnsNotFound_WhenIdDoesNotExist() {
            // Arrange
            var context = GetDbContextWithData();
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();

            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

            var controller = new SizesController(context, mockHub.Object);

            // Act
            var result = await controller.DeleteSize(999); 

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

    }
}
