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
            // Mục tiêu: Kiểm tra xem API có trả về danh sách nhà cung cấp khi có dữ liệu hay không.
            // Input: Không có tham số đầu vào, chỉ cần dữ liệu trong database.
            // Expected Output: Trả về danh sách nhà cung cấp (danh sách không rỗng).

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
            // Mục tiêu: Kiểm tra xem API có thêm nhà cung cấp thành công khi dữ liệu hợp lệ hay không.
            // Input: UploadNhaCungCap với Ten, SDT, ThongTin, DiaChi hợp lệ.
            // Expected Output: Trả về thông báo thành công, nhà cung cấp được thêm vào database.

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

        // NhaCungCap03: Thêm nhà cung cấp với dữ liệu không hợp lệ (thiếu thông tin bắt buộc)
        [Fact]
        public async Task NhaCungCap03_PostNhaCungCap_ThrowsException_WhenDataIsInvalid()
        {
            // Mục tiêu: Kiểm tra xem API có xử lý đúng khi dữ liệu không hợp lệ (ví dụ: thiếu tên nhà cung cấp) hay không.
            // Input: UploadNhaCungCap với Ten = null (không hợp lệ).
            // Expected Output: Ném ra lỗi (vì API không kiểm tra dữ liệu đầu vào, nhưng trong thực tế nên có validation).

            // Arrange - Chuẩn bị dữ liệu không hợp lệ
            var upload = new UploadNhaCungCap
            {
                Ten = null, // Thiếu tên (giả sử đây là trường bắt buộc)
                SDT = "0123456789",
                ThongTin = "Thông tin C",
                DiaChi = "789 Đường C, Hà Nội"
            };

            // Act - Gọi API
            var result = await _controller.PostNhaCungCapAsync(upload);

            // Assert - Kiểm tra phản hồi của API
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode); // Kiểm tra mã lỗi 400

            var error = badRequestResult.Value;
            var messageProperty = error.GetType().GetProperty("message");
            var messageValue = messageProperty.GetValue(error) as string;
            Assert.Equal("Tên nhà cung cấp không được để trống", messageValue);
        }

        // NhaCungCap04: Cập nhật nhà cung cấp khi Id tồn tại
        [Fact]
        public async Task NhaCungCap04_PutNhaCungCap_Success_WhenIdExists()
        {
            // Mục tiêu: Kiểm tra xem API có cập nhật nhà cung cấp thành công khi Id tồn tại hay không.
            // Input: Id = 1 (tồn tại), UploadNhaCungCap với thông tin mới.
            // Expected Output: Trả về thông báo thành công, thông tin nhà cung cấp được cập nhật trong database.

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

        // NhaCungCap05: Cập nhật nhà cung cấp khi Id không tồn tại
        [Fact]
        public async Task NhaCungCap05_PutNhaCungCap_ThrowsException_WhenIdDoesNotExist()
        {
            // Mục tiêu: Kiểm tra xem API có xử lý đúng khi Id không tồn tại hay không.
            // Input: Id = 999 (không tồn tại), UploadNhaCungCap với thông tin mới.
            // Expected Output: Ném ra lỗi (vì Id không tồn tại).

            // Arrange - Chuẩn bị dữ liệu
            var upload = new UploadNhaCungCap
            {
                Ten = "Nhà cung cấp E",
                SDT = "0123456789",
                ThongTin = "Thông tin E",
                DiaChi = "123 Đường E, Hà Nội"
            };

            // Act & Assert - Gọi API và kiểm tra lỗi
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
                await _controller.PutNhaCungCapAsync(upload, 999));

            
        }

        // ---------------------- DELETE ------------------------
        // NhaCungCap06: Xóa nhà cung cấp khi Id tồn tại và không có sản phẩm liên quan
        [Fact]
        public async Task NhaCungCap06_DeleteNhaCungCap_Success_WhenIdExistsAndNoRelatedProducts()
        {
            // Mục tiêu: Kiểm tra xem API có xóa nhà cung cấp thành công khi Id tồn tại và không có sản phẩm liên quan hay không.
            // Input: Id = 1 (tồn tại, không có sản phẩm liên quan).
            // Expected Output: Trả về thông báo thành công, nhà cung cấp bị xóa khỏi database, BroadcastMessage được gọi.

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

        // NhaCungCap07: Xóa nhà cung cấp khi Id tồn tại và có sản phẩm liên quan
        [Fact]
        public async Task NhaCungCap07_DeleteNhaCungCap_Success_WhenIdExistsAndHasRelatedProducts()
        {
            // Mục tiêu: Kiểm tra xem API có xóa nhà cung cấp và các sản phẩm liên quan thành công hay không.
            // Input: Id = 1 (tồn tại, có sản phẩm liên quan).
            // Expected Output: Trả về thông báo thành công, nhà cung cấp và sản phẩm liên quan bị xóa, BroadcastMessage được gọi.

            // Arrange - Chuẩn bị dữ liệu
            var nhaCungCap = new NhaCungCap
            {
                Ten = "Nhà cung cấp G",
                SDT = "0123456789",
                ThongTin = "Thông tin G",
                DiaChi = "123 Đường G, Hà Nội"
            };
            _context.NhaCungCaps.Add(nhaCungCap);
            await _context.SaveChangesAsync();

            var sanPham = new SanPham
            {
                Ten = "Sản phẩm G",
                GiaBan = 100000,
                Id_NhaCungCap = nhaCungCap.Id
            };
            _context.SanPhams.Add(sanPham);
            await _context.SaveChangesAsync();

            // Act - Gọi API để xóa
            var result = await _controller.DeleteNhaCungCapAsync(nhaCungCap.Id);

            // Assert - Kiểm tra kết quả
            var okResult = Assert.IsType<OkResult>(result); // Kiểm tra API trả về thông báo thành công

            // Kiểm tra trong database
            var deletedNhaCungCap = await _context.NhaCungCaps.FindAsync(nhaCungCap.Id);
            Assert.Null(deletedNhaCungCap); // Kiểm tra nhà cung cấp đã bị xóa

            var deletedSanPham = await _context.SanPhams.FindAsync(sanPham.Id);
            Assert.Null(deletedSanPham); // Kiểm tra sản phẩm liên quan đã bị xóa
        }

        // NhaCungCap08: Xóa nhà cung cấp khi Id không tồn tại
        [Fact]
        public async Task NhaCungCap08_DeleteNhaCungCap_ThrowsException_WhenIdDoesNotExist()
        {
            // Mục tiêu: Kiểm tra xem API có xử lý đúng khi Id không tồn tại hay không.
            // Input: Id = 999 (không tồn tại).
            // Expected Output: Ném ra lỗi (vì Id không tồn tại).

            // Act & Assert - Gọi API và kiểm tra lỗi
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
                await _controller.DeleteNhaCungCapAsync(999));

            // Đề xuất: Trong controller, nên kiểm tra Id và trả về NotFound() nếu không tìm thấy.
        }

        // NhaCungCap09: Xóa nhà cung cấp khi Id âm (không hợp lệ)
        [Fact]
        public async Task NhaCungCap09_DeleteNhaCungCap_ThrowsException_WhenIdIsNegative()
        {
            // Mục tiêu: Kiểm tra xem API có xử lý đúng khi Id là số âm (không hợp lệ) hay không.
            // Input: Id = -1 (không hợp lệ).
            // Expected Output: Ném ra lỗi (vì Id không hợp lệ).

            // Act & Assert - Gọi API và kiểm tra lỗi
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
                await _controller.DeleteNhaCungCapAsync(-1));

            // Đề xuất: Trong controller, nên kiểm tra Id và trả về BadRequest() nếu Id không hợp lệ.
        }
    }
}
