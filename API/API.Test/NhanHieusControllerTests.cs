using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helper.SignalR;
using API.Models;
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
    public class NhanHieusControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly NhanHieusController _controller;

        public NhanHieusControllerTests() : base()
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
            _controller = new NhanHieusController(_context, _hubContext);
        }

        // ---------------------- GET ALL ------------------------
        // NhanHieu01: Lấy danh sách nhãn hiệu khi có dữ liệu
        [Fact]
        public async Task NhanHieu01_GetThuongHieus_ReturnsList_WhenDataExists()
        {
            // Mục tiêu: Kiểm tra xem API có trả về danh sách nhãn hiệu khi có dữ liệu hay không.
            // Input: Không có tham số đầu vào, chỉ cần dữ liệu trong database.
            // Expected Output: Trả về danh sách nhãn hiệu (danh sách không rỗng).

            // Arrange - Chuẩn bị dữ liệu
            var nhanHieu1 = new NhanHieu
            {
                Ten = "Nhãn hiệu A",
                DateCreate = DateTime.Now
            };
            var nhanHieu2 = new NhanHieu
            {
                Ten = "Nhãn hiệu B",
                DateCreate = DateTime.Now
            };
            _context.NhanHieus.AddRange(nhanHieu1, nhanHieu2);
            await _context.SaveChangesAsync();

            // Act - Gọi API
            var result = await _controller.GetThuongHieus();

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NhanHieu>>>(result);
            var data = Assert.IsAssignableFrom<IEnumerable<NhanHieu>>(actionResult.Value);
            Assert.NotEmpty(data); // Kiểm tra danh sách không rỗng
            Assert.Contains(data, n => n.Ten == "Nhãn hiệu A");
            Assert.Contains(data, n => n.Ten == "Nhãn hiệu B");
        }

        // ---------------------- GET BY ID ------------------------
        // NhanHieu02: Lấy nhãn hiệu theo Id khi Id tồn tại
        [Fact]
        public async Task NhanHieu02_GetThuongHieu_ReturnsNhanHieu_WhenIdExists()
        {
            // Mục tiêu: Kiểm tra xem API có trả về thông tin nhãn hiệu khi Id tồn tại hay không.
            // Input: Id = 1 (tồn tại trong database).
            // Expected Output: Trả về thông tin nhãn hiệu (tên và ngày tạo).

            // Arrange - Chuẩn bị dữ liệu
            var nhanHieu = new NhanHieu
            {
                Ten = "Nhãn hiệu C",
                DateCreate = DateTime.Now
            };
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            // Act - Gọi API với Id = 1
            var result = await _controller.GetThuongHieu(nhanHieu.Id);

            // Assert - Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<NhanHieu>>(result);
            var data = Assert.IsType<NhanHieu>(actionResult.Value);
            Assert.Equal("Nhãn hiệu C", data.Ten);
            Assert.Equal(nhanHieu.DateCreate, data.DateCreate);
        }

        // NhanHieu03: Lấy nhãn hiệu theo Id khi Id không tồn tại
        [Fact]
        public async Task NhanHieu03_GetThuongHieu_ReturnsNotFound_WhenIdDoesNotExist()
        {
            // Mục tiêu: Kiểm tra xem API có trả về thông báo không tìm thấy khi Id không tồn tại hay không.
            // Input: Id = 999 (không tồn tại trong database).
            // Expected Output: Trả về thông báo không tìm thấy.

            // Act - Gọi API với Id không tồn tại
            var result = await _controller.GetThuongHieu(999);

            // Assert - Kiểm tra kết quả
            var notFoundResult = Assert.IsType<NotFoundResult>(result.Result); // Kiểm tra API trả về thông báo không tìm thấy
        }

        // NhanHieu04: Lấy nhãn hiệu theo Id âm (không hợp lệ)
        [Fact]
        public async Task NhanHieu04_GetThuongHieu_ReturnsNotFound_WhenIdIsNegative()
        {
            // Mục tiêu: Kiểm tra xem API có xử lý đúng khi Id là số âm (không hợp lệ) hay không.
            // Input: Id = -1 (không hợp lệ).
            // Expected Output: Trả về thông báo không tìm thấy.

            // Act - Gọi API với Id âm
            var result = await _controller.GetThuongHieu(-1);

            // Assert - Kiểm tra kết quả
            var notFoundResult = Assert.IsType<NotFoundResult>(result.Result); // Kiểm tra API trả về thông báo không tìm thấy
        }

        // ---------------------- POST ------------------------
        // NhanHieu05: Thêm nhãn hiệu với dữ liệu hợp lệ
        [Fact]
        public async Task NhanHieu05_PostNhanHieu_Success_WhenDataIsValid()
        {
            // Mục tiêu: Kiểm tra xem API có thêm nhãn hiệu thành công khi dữ liệu hợp lệ hay không.
            // Input: UploadBrand với Name hợp lệ.
            // Expected Output: Trả về thông báo thành công, nhãn hiệu được thêm vào database, thông báo "Add" được tạo.

            // Arrange - Chuẩn bị dữ liệu
            var upload = new UploadBrand
            {
                Name = "Nhãn hiệu D"
            };

            // Act - Gọi API để thêm nhãn hiệu
            var result = await _controller.PostNhanHieu(upload);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<ActionResult<NhanHieu>>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var nhanHieu = await _context.NhanHieus.FirstOrDefaultAsync(n => n.Ten == "Nhãn hiệu D");
            Assert.NotNull(nhanHieu); // Kiểm tra nhãn hiệu đã được thêm
            Assert.Equal("Nhãn hiệu D", nhanHieu.Ten);
            Assert.NotNull(nhanHieu.DateCreate); // Kiểm tra ngày tạo không null

            // Kiểm tra thông báo
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "Nhãn hiệu D");
            Assert.NotNull(notification);
            Assert.Equal("Add", notification.TranType);
        }

        // NhanHieu06: Thêm nhãn hiệu với dữ liệu không hợp lệ (tên rỗng)
        [Fact]
        public async Task NhanHieu06_PostNhanHieu_ThrowsException_WhenDataIsInvalid()
        {
            // Mục tiêu: Kiểm tra xem API có xử lý đúng khi dữ liệu không hợp lệ (tên rỗng) hay không.
            // Input: UploadBrand với Name = null (không hợp lệ).
            // Expected Output: API vẫn thêm nhãn hiệu (vì không có validation), nhưng trong thực tế nên ném lỗi.

            // Arrange - Chuẩn bị dữ liệu không hợp lệ
            var upload = new UploadBrand
            {
                Name = null // Tên rỗng (giả sử đây là trường bắt buộc)
            };

            // Act - Gọi API
            var result = await _controller.PostNhanHieu(upload);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<ActionResult<NhanHieu>>(result); // API vẫn trả về thành công (vì không có validation)

            // Kiểm tra trong database
            var nhanHieu = await _context.NhanHieus.FirstOrDefaultAsync();
            Assert.NotNull(nhanHieu);
            Assert.Null(nhanHieu.Ten); // Tên là null (không mong muốn)


        }

        // ---------------------- PUT ------------------------
        // NhanHieu08: Cập nhật nhãn hiệu khi Id tồn tại
        [Fact]
        public async Task NhanHieu08_PutNhanHieu_Success_WhenIdExists()
        {
            // Mục tiêu: Kiểm tra xem API có cập nhật nhãn hiệu thành công khi Id tồn tại hay không.
            // Input: Id = 1 (tồn tại), UploadBrand với Name mới.
            // Expected Output: Trả về thông báo thành công, thông tin nhãn hiệu được cập nhật, thông báo "Edit" được tạo.

            // Arrange - Chuẩn bị dữ liệu
            var nhanHieu = new NhanHieu
            {
                Ten = "Nhãn hiệu E",
                DateCreate = DateTime.Now.AddDays(-1)
            };
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            var upload = new UploadBrand
            {
                Name = "Nhãn hiệu E Updated"
            };

            // Act - Gọi API để cập nhật
            var result = await _controller.PutNhanHieu(nhanHieu.Id, upload);

            // Assert - Kiểm tra kết quả
            var noContentResult = Assert.IsType<NoContentResult>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var updatedNhanHieu = await _context.NhanHieus.FindAsync(nhanHieu.Id);
            Assert.Equal("Nhãn hiệu E Updated", updatedNhanHieu.Ten);
            Assert.True(updatedNhanHieu.DateCreate > nhanHieu.DateCreate); // Kiểm tra ngày cập nhật mới hơn

            // Kiểm tra thông báo
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "Nhãn hiệu E Updated");
            Assert.NotNull(notification);
            Assert.Equal("Edit", notification.TranType);
        }

        // NhanHieu09: Cập nhật nhãn hiệu khi Id không tồn tại
        [Fact]
        public async Task NhanHieu09_PutNhanHieu_ReturnsNotFound_WhenIdDoesNotExist()
        {
            // Mục tiêu: Kiểm tra xem API có trả về thông báo không tìm thấy khi Id không tồn tại hay không.
            // Input: Id = 999 (không tồn tại), UploadBrand với Name mới.
            // Expected Output: Trả về thông báo không tìm thấy.

            // Arrange - Chuẩn bị dữ liệu
            var upload = new UploadBrand
            {
                Name = "Nhãn hiệu F"
            };

            // Act - Gọi API với Id không tồn tại
            var result = await _controller.PutNhanHieu(999, upload);

            // Assert - Kiểm tra kết quả
            var notFoundResult = Assert.IsType<NotFoundResult>(result); // Kiểm tra API trả về thông báo không tìm thấy
        }

        // NhanHieu10: Cập nhật nhãn hiệu với dữ liệu không hợp lệ (tên rỗng)
        [Fact]
        public async Task NhanHieu10_PutNhanHieu_ThrowsException_WhenDataIsInvalid()
        {
            // Mục tiêu: Kiểm tra xem API có xử lý đúng khi dữ liệu không hợp lệ (tên rỗng) hay không.
            // Input: Id = 1 (tồn tại), UploadBrand với Name = null (không hợp lệ).
            // Expected Output: API vẫn cập nhật (vì không có validation), nhưng trong thực tế nên ném lỗi.

            // Arrange - Chuẩn bị dữ liệu
            var nhanHieu = new NhanHieu
            {
                Ten = "Nhãn hiệu G",
                DateCreate = DateTime.Now
            };
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            var upload = new UploadBrand
            {
                Name = null // Tên rỗng (giả sử đây là trường bắt buộc)
            };

            // Act - Gọi API
            var result = await _controller.PutNhanHieu(nhanHieu.Id, upload);

            // Assert - Kiểm tra kết quả
            var noContentResult = Assert.IsType<NoContentResult>(result); // API vẫn trả về thành công (vì không có validation)

            // Kiểm tra trong database
            var updatedNhanHieu = await _context.NhanHieus.FindAsync(nhanHieu.Id);
            Assert.Null(updatedNhanHieu.Ten); // Tên là null (không mong muốn)

            // Đề xuất: Thêm validation trong controller để ném lỗi nếu Name = null.
        }

        // ---------------------- DELETE ------------------------
        // NhanHieu11: Xóa nhãn hiệu khi Id tồn tại và không có sản phẩm liên quan
        [Fact]
        public async Task NhanHieu11_DeleteThuongHieu_Success_WhenIdExistsAndNoRelatedProducts()
        {
            // Mục tiêu: Kiểm tra xem API có xóa nhãn hiệu thành công khi Id tồn tại và không có sản phẩm liên quan hay không.
            // Input: Id = 1 (tồn tại, không có sản phẩm liên quan).
            // Expected Output: Trả về thông báo thành công, nhãn hiệu bị xóa, thông báo "Delete" được tạo, BroadcastMessage được gọi.

            // Arrange - Chuẩn bị dữ liệu
            var nhanHieu = new NhanHieu
            {
                Ten = "Nhãn hiệu H",
                DateCreate = DateTime.Now
            };
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            // Act - Gọi API để xóa
            var result = await _controller.DeleteThuongHieu(nhanHieu.Id);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<OkResult>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var deletedNhanHieu = await _context.NhanHieus.FindAsync(nhanHieu.Id);
            Assert.Null(deletedNhanHieu); // Kiểm tra nhãn hiệu đã bị xóa

            // Kiểm tra thông báo
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "Nhãn hiệu H");
            Assert.NotNull(notification);
            Assert.Equal("Delete", notification.TranType);
        }

        // NhanHieu12: Xóa nhãn hiệu khi Id tồn tại và có sản phẩm liên quan
        [Fact]
        public async Task NhanHieu12_DeleteThuongHieu_Success_WhenIdExistsAndHasRelatedProducts()
        {
            // Mục tiêu: Kiểm tra xem API có xóa nhãn hiệu và các sản phẩm liên quan thành công hay không.
            // Input: Id = 1 (tồn tại, có sản phẩm liên quan).
            // Expected Output: Trả về thông báo thành công, nhãn hiệu và sản phẩm liên quan bị xóa, thông báo "Delete" được tạo, BroadcastMessage được gọi.

            // Arrange - Chuẩn bị dữ liệu
            var nhanHieu = new NhanHieu
            {
                Ten = "Nhãn hiệu I",
                DateCreate = DateTime.Now
            };
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            var sanPham = new SanPham
            {
                Ten = "Sản phẩm I",
                GiaBan = 100000,
                Id_NhanHieu = nhanHieu.Id // Liên quan đến nhãn hiệu
            };
            _context.SanPhams.Add(sanPham);
            await _context.SaveChangesAsync();

            // Act - Gọi API để xóa
            var result = await _controller.DeleteThuongHieu(nhanHieu.Id);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<OkResult>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var deletedNhanHieu = await _context.NhanHieus.FindAsync(nhanHieu.Id);
            Assert.Null(deletedNhanHieu); // Kiểm tra nhãn hiệu đã bị xóa

            var deletedSanPham = await _context.SanPhams.FindAsync(sanPham.Id);
            Assert.Null(deletedSanPham); // Kiểm tra sản phẩm liên quan đã bị xóa

            // Kiểm tra thông báo
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "Nhãn hiệu I");
            Assert.NotNull(notification);
            Assert.Equal("Delete", notification.TranType);
        }

        // NhanHieu13: Xóa nhãn hiệu khi Id không tồn tại
        [Fact]
        public async Task NhanHieu13_DeleteThuongHieu_ThrowsException_WhenIdDoesNotExist()
        {
            // Mục tiêu: Kiểm tra xem API có xử lý đúng khi Id không tồn tại hay không.
            // Input: Id = 999 (không tồn tại).
            // Expected Output: Ném ra lỗi (vì Id không tồn tại).

            // Act & Assert - Gọi API và kiểm tra lỗi
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
                await _controller.DeleteThuongHieu(9999));

            // Đề xuất: Trong controller, nên kiểm tra Id và trả về NotFound() nếu không tìm thấy.
        }

        // NhanHieu14: Xóa nhãn hiệu khi Id âm (không hợp lệ)
        [Fact]
        public async Task NhanHieu14_DeleteThuongHieu_ThrowsException_WhenIdIsNegative()
        {
            // Mục tiêu: Kiểm tra xem API có xử lý đúng khi Id là số âm (không hợp lệ) hay không.
            // Input: Id = -1 (không hợp lệ).
            // Expected Output: Ném ra lỗi (vì Id không hợp lệ).

            // Act & Assert - Gọi API và kiểm tra lỗi
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
                await _controller.DeleteThuongHieu(-1));

            // Đề xuất: Trong controller, nên kiểm tra Id và trả về BadRequest() nếu Id không hợp lệ.
        }
    }
}

