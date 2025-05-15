using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers;
using API.Data;
using API.Dtos;
using API.Helper;
using API.Helper.SignalR;
using API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace API.Test
{
    public class TaoPhieuNhapsControllerTests
    {
        private readonly TaoPhieuNhapsController _controller;
        private readonly DPContext _context;
        private readonly Mock<IHubContext<BroadcastHub, IHubClient>> _hubContextMock;

        public TaoPhieuNhapsControllerTests()
        {
            // Thiết lập in-memory database để mô phỏng DPContext
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            var options = new DbContextOptionsBuilder<DPContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .UseInternalServiceProvider(serviceProvider)
                .Options;

            _context = new DPContext(options);

            // Mock IHubContext để giả lập SignalR
            _hubContextMock = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var hubClientsMock = new Mock<IHubClient>();
            _hubContextMock.Setup(h => h.Clients.All).Returns(hubClientsMock.Object);

            // Khởi tạo controller với context và hub mock
            _controller = new TaoPhieuNhapsController(_context, _hubContextMock.Object);

            // Thiết lập HttpContext cho controller
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        // --- Tests for GetAllSanPhams ---
        // TPNC01
        [Fact]
        public async Task GetAllSanPhams_WithData_ReturnsNonEmptyList()
        {
            // Kiểm tra nhánh: Database có dữ liệu, trả về danh sách sản phẩm không rỗng
            // Arrange: Thêm một sản phẩm vào database
            var sanPham = new SanPham { Id = 1, Ten = "SP1", Id_NhaCungCap = 1 };
            await _context.SanPhams.AddAsync(sanPham);
            await _context.SaveChangesAsync();

            // Act: Gọi phương thức GetAllSanPhams
            var result = await _controller.GetAllSanPhams(new UploadNhaCungCap());

            // Assert: Kiểm tra trả về danh sách có một sản phẩm
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPham>>>(result);
            var returnValue = Assert.IsType<List<SanPham>>(actionResult.Value);
            Assert.Single(returnValue);
            Assert.Equal("SP1", returnValue.First().Ten);
        }

        // TPNC02
        [Fact]
        public async Task GetAllSanPhams_WithNoData_ReturnsEmptyList()
        {
            // Kiểm tra nhánh: Database rỗng, trả về danh sách sản phẩm rỗng
            // Arrange: Database rỗng
            // Act: Gọi phương thức GetAllSanPhams
            var result = await _controller.GetAllSanPhams(new UploadNhaCungCap());

            // Assert: Kiểm tra trả về danh sách rỗng
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPham>>>(result);
            var returnValue = Assert.IsType<List<SanPham>>(actionResult.Value);
            Assert.Empty(returnValue);
        }

        // --- Tests for GetAllSanPhamBienThe ---
        // TPNC03
        [Fact]
        public async Task GetAllSanPhamBienThe_WithData_ReturnsNonEmptyList()
        {
            // Kiểm tra nhánh: Database có dữ liệu, trả về danh sách biến thể không rỗng
            // Arrange: Thêm một biến thể sản phẩm
            var sanPhamBienThe = new SanPhamBienThe { Id = 1, Id_SanPham = 1, SoLuongTon = 10 };
            await _context.SanPhamBienThes.AddAsync(sanPhamBienThe);
            await _context.SaveChangesAsync();

            // Act: Gọi phương thức GetAllSanPhamBienThe
            var result = await _controller.GetAllSanPhamBienThe();

            // Assert: Kiểm tra trả về danh sách có một biến thể
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPhamBienThe>>>(result);
            var returnValue = Assert.IsType<List<SanPhamBienThe>>(actionResult.Value);
            Assert.Single(returnValue);
            Assert.Equal(1, returnValue.First().Id);
        }

        // TPNC04
        [Fact]
        public async Task GetAllSanPhamBienThe_WithNoData_ReturnsEmptyList()
        {
            // Kiểm tra nhánh: Database rỗng, trả về danh sách biến thể rỗng
            // Arrange: Database rỗng
            // Act: Gọi phương thức GetAllSanPhamBienThe
            var result = await _controller.GetAllSanPhamBienThe();

            // Assert: Kiểm tra trả về danh sách rỗng
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPhamBienThe>>>(result);
            var returnValue = Assert.IsType<List<SanPhamBienThe>>(actionResult.Value);
            Assert.Empty(returnValue);
        }

        // --- Tests for GetAllPhieuNhap ---
        // TPNC05
        [Fact]
        public async Task GetAllPhieuNhap_WithValidData_ReturnsList()
        {
            // Kiểm tra nhánh: Database có dữ liệu đầy đủ, trả về danh sách phiếu nhập
            // Arrange: Thêm nhà cung cấp, người dùng, và phiếu nhập
            var nhaCungCap = new NhaCungCap { Id = 1, Ten = "NCC1" };
            var user = new AppUser { Id = "user1", FirstName = "John", LastName = "Doe" };
            var phieuNhap = new PhieuNhapHang
            {
                Id = 1,
                Id_NhaCungCap = 1,
                NguoiLapPhieu = "user1",
                SoChungTu = "PN001",
                TongTien = 100000,
                GhiChu = "Test",
                NgayTao = DateTime.Now
            };
            await _context.NhaCungCaps.AddAsync(nhaCungCap);
            await _context.AppUsers.AddAsync(user);
            await _context.PhieuNhapHangs.AddAsync(phieuNhap);
            await _context.SaveChangesAsync();

            // Act: Gọi phương thức GetAllPhieuNhap
            var result = await _controller.GetAllPhieuNhap();

            // Assert: Kiểm tra trả về danh sách có một phiếu nhập với thông tin đúng
            var actionResult = Assert.IsType<ActionResult<IEnumerable<PhieuNhapHangNhaCungCap>>>(result);
            var returnValue = Assert.IsType<List<PhieuNhapHangNhaCungCap>>(actionResult.Value);
            Assert.Single(returnValue);
            var phieu = returnValue.First();
            Assert.Equal("NCC1", phieu.TenNhaCungCap);
            Assert.Equal("John Doe", phieu.NguoiLapPhieu);
        }

        // TPNC06
        [Fact]
        public async Task GetAllPhieuNhap_WithNoData_ReturnsEmptyList()
        {
            // Kiểm tra nhánh: Database rỗng, trả về danh sách phiếu nhập rỗng
            // Arrange: Database rỗng
            // Act: Gọi phương thức GetAllPhieuNhap
            var result = await _controller.GetAllPhieuNhap();

            // Assert: Kiểm tra trả về danh sách rỗng
            var actionResult = Assert.IsType<ActionResult<IEnumerable<PhieuNhapHangNhaCungCap>>>(result);
            var returnValue = Assert.IsType<List<PhieuNhapHangNhaCungCap>>(actionResult.Value);
            Assert.Empty(returnValue);
        }

        // TPNC07
        [Fact]
        public async Task GetAllPhieuNhap_WithMissingUserData_ReturnsEmptyList()
        {
            // Kiểm tra nhánh: Thiếu AppUsers, LINQ join không khớp, trả về danh sách rỗng
            // Arrange: Thêm nhà cung cấp và phiếu nhập, nhưng thiếu người dùng
            var nhaCungCap = new NhaCungCap { Id = 1, Ten = "NCC1" };
            var phieuNhap = new PhieuNhapHang
            {
                Id = 1,
                Id_NhaCungCap = 1,
                NguoiLapPhieu = "user1",
                SoChungTu = "WYPN001",
                TongTien = 100000
            };
            await _context.NhaCungCaps.AddAsync(nhaCungCap);
            await _context.PhieuNhapHangs.AddAsync(phieuNhap);
            await _context.SaveChangesAsync();

            // Act: Gọi phương thức GetAllPhieuNhap
            var result = await _controller.GetAllPhieuNhap();

            // Assert: Kiểm tra trả về danh sách rỗng do join thất bại
            var actionResult = Assert.IsType<ActionResult<IEnumerable<PhieuNhapHangNhaCungCap>>>(result);
            var returnValue = Assert.IsType<List<PhieuNhapHangNhaCungCap>>(actionResult.Value);
            Assert.Empty(returnValue);
        }

        // --- Tests for GetSanPhamNhaCungCaps ---
        // TPNC08
        [Fact]
        public async Task GetSanPhamNhaCungCaps_WithProducts_ReturnsProductList()
        {
            // Kiểm tra nhánh: Nhà cung cấp tồn tại và có sản phẩm, trả về danh sách sản phẩm
            // Arrange: Thêm nhà cung cấp và sản phẩm
            var nhaCungCap = new NhaCungCap { Id = 1, Ten = "NCC1" };
            var sanPham = new SanPham { Id = 1, Ten = "SP1", Id_NhaCungCap = 1 };
            await _context.NhaCungCaps.AddAsync(nhaCungCap);
            await _context.SanPhams.AddAsync(sanPham);
            await _context.SaveChangesAsync();

            // Act: Gọi phương thức GetSanPhamNhaCungCaps
            var result = await _controller.GetSanPhamNhaCungCaps(nhaCungCap);

            // Assert: Kiểm tra trả về danh sách có một sản phẩm
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPham>>>(result);
            var returnValue = Assert.IsType<List<SanPham>>(actionResult.Value);
            Assert.Single(returnValue);
            Assert.Equal("SP1", returnValue.First().Ten);
        }

        // TPNC09
        [Fact]
        public async Task GetSanPhamNhaCungCaps_WithNoProducts_ReturnsEmptyList()
        {
            // Kiểm tra nhánh: Nhà cung cấp tồn tại nhưng không có sản phẩm, trả về danh sách rỗng
            // Arrange: Thêm nhà cung cấp, không có sản phẩm
            var nhaCungCap = new NhaCungCap { Id = 1, Ten = "NCC1" };
            await _context.NhaCungCaps.AddAsync(nhaCungCap);
            await _context.SaveChangesAsync();

            // Act: Gọi phương thức GetSanPhamNhaCungCaps
            var result = await _controller.GetSanPhamNhaCungCaps(nhaCungCap);

            // Assert: Kiểm tra trả về danh sách rỗng
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPham>>>(result);
            var returnValue = Assert.IsType<List<SanPham>>(actionResult.Value);
            Assert.Empty(returnValue);
        }

        // TPNC10
        [Fact]
        public async Task GetSanPhamNhaCungCaps_WithNonExistentNhaCungCap_ReturnsEmptyList()
        {
            // Kiểm tra nhánh: Nhà cung cấp không tồn tại, trả về danh sách rỗng
            // Arrange: Nhà cung cấp không tồn tại
            var nhaCungCap = new NhaCungCap { Id = 999 };

            // Act: Gọi phương thức GetSanPhamNhaCungCaps
            var result = await _controller.GetSanPhamNhaCungCaps(nhaCungCap);

            // Assert: Kiểm tra trả về danh sách rỗng
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPham>>>(result);
            var returnValue = Assert.IsType<List<SanPham>>(actionResult.Value);
            Assert.Empty(returnValue);
        }

        // --- Tests for GetNhaCungCaps ---
        // TPNC11
        [Fact]
        public async Task GetNhaCungCaps_WithExistingId_ReturnsNhaCungCap()
        {
            // Kiểm tra nhánh: ID nhà cung cấp tồn tại, trả về nhà cung cấp
            // Arrange: Thêm nhà cung cấp
            var nhaCungCap = new NhaCungCap { Id = 1, Ten = "NCC1", SDT = "123456789", DiaChi = "Hanoi" };
            await _context.NhaCungCaps.AddAsync(nhaCungCap);
            await _context.SaveChangesAsync();
            var inputNhaCungCap = new NhaCungCap { Id = 1 };

            // Act: Gọi phương thức GetNhaCungCaps
            var result = await _controller.GetNhaCungCaps(inputNhaCungCap);

            // Assert: Kiểm tra trả về nhà cung cấp đúng
            var actionResult = Assert.IsType<ActionResult<NhaCungCap>>(result);
            var returnValue = Assert.IsType<NhaCungCap>(actionResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Equal("NCC1", returnValue.Ten);
        }

        // TPNC12
        [Fact]
        public async Task GetNhaCungCaps_WithNonExistingId_ReturnsNotFound()
        {
            // Kiểm tra nhánh: ID nhà cung cấp không tồn tại, trả về NotFound
            // Arrange: ID không tồn tại
            var inputNhaCungCap = new NhaCungCap { Id = 999 };

            // Act: Gọi phương thức GetNhaCungCaps
            var result = await _controller.GetNhaCungCaps(inputNhaCungCap);

            // Assert: Kiểm tra trả về NotFound
            Assert.IsType<NotFoundResult>(result.Result);
        }

        // --- Tests for GetTenSanPhamBienThe ---
        // TPNC13
        [Fact]
        public async Task GetTenSanPhamBienThe_WithValidData_ReturnsList()
        {
            // Kiểm tra nhánh: Sản phẩm có biến thể và dữ liệu liên quan, trả về danh sách biến thể
            // Arrange: Thêm sản phẩm, loại, màu sắc, kích thước, và biến thể
            var sanPham = new SanPham { Id = 1, Ten = "SP1", Id_Loai = 1, GiaNhap = 50000 };
            var loai = new Loai { Id = 1, Ten = "Loai1" };
            var mauSac = new MauSac { Id = 1, MaMau = "Red" };
            var size = new Size { Id = 1, TenSize = "M" };
            var sanPhamBienThe = new SanPhamBienThe { Id = 1, Id_SanPham = 1, Id_Mau = 1, SizeId = 1 };
            await _context.SanPhams.AddAsync(sanPham);
            await _context.Loais.AddAsync(loai);
            await _context.MauSacs.AddAsync(mauSac);
            await _context.Sizes.AddAsync(size);
            await _context.SanPhamBienThes.AddAsync(sanPhamBienThe);
            await _context.SaveChangesAsync();
            var inputSanPham = new SanPham { Id = 1 };

            // Act: Gọi phương thức GetTenSanPhamBienThe
            var result = await _controller.GetTenSanPhamBienThe(inputSanPham);

            // Assert: Kiểm tra trả về danh sách có một biến thể với thông tin đúng
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPhamBienTheMauSizeLoai>>>(result);
            var returnValue = Assert.IsType<List<SanPhamBienTheMauSizeLoai>>(actionResult.Value);
            Assert.Single(returnValue);
            var bienThe = returnValue.First();
            Assert.Equal("Id: 1 Tên: SP1 Loai1 Red", bienThe.TenSanPhamBienTheMauSize);
            Assert.Equal(50000, bienThe.GiaNhap);
        }


        // TPNC14
        [Fact]
        public async Task GetTenSanPhamBienThe_WithNoVariants_ReturnsEmptyList()
        {
            // Kiểm tra nhánh: Sản phẩm không có biến thể, trả về danh sách rỗng
            // Arrange: Thêm sản phẩm, không có biến thể
            var sanPham = new SanPham { Id = 1, Ten = "SP1", Id_Loai = 1 };
            await _context.SanPhams.AddAsync(sanPham);
            await _context.SaveChangesAsync();
            var inputSanPham = new SanPham { Id = 1 };

            // Act: Gọi phương thức GetTenSanPhamBienThe
            var result = await _controller.GetTenSanPhamBienThe(inputSanPham);

            // Assert: Kiểm tra trả về danh sách rỗng
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPhamBienTheMauSizeLoai>>>(result);
            var returnValue = Assert.IsType<List<SanPhamBienTheMauSizeLoai>>(actionResult.Value);
            Assert.Empty(returnValue);
        }


        // TPNC15
        [Fact]
        public async Task GetTenSanPhamBienThe_WithMissingRelatedData_ReturnsEmptyList()
        {
            // Kiểm tra nhánh: Thiếu dữ liệu liên quan (MauSacs), LINQ join không khớp, trả về danh sách rỗng
            // Arrange: Thêm sản phẩm và biến thể, thiếu màu sắc
            var sanPham = new SanPham { Id = 1, Ten = "SP1", Id_Loai = 1 };
            var sanPhamBienThe = new SanPhamBienThe { Id = 1, Id_SanPham = 1, Id_Mau = 1, SizeId = 1 };
            await _context.SanPhams.AddAsync(sanPham);
            await _context.SanPhamBienThes.AddAsync(sanPhamBienThe);
            await _context.SaveChangesAsync();
            var inputSanPham = new SanPham { Id = 1 };

            // Act: Gọi phương thức GetTenSanPhamBienThe
            var result = await _controller.GetTenSanPhamBienThe(inputSanPham);

            // Assert: Kiểm tra trả về danh sách rỗng do join thất bại
            var actionResult = Assert.IsType<ActionResult<IEnumerable<SanPhamBienTheMauSizeLoai>>>(result);
            var returnValue = Assert.IsType<List<SanPhamBienTheMauSizeLoai>>(actionResult.Value);
            Assert.Empty(returnValue);
        }

        // --- Tests for PostTaoPhieuNhap ---
        // TPNC16
        [Fact]
        public async Task PostTaoPhieuNhap_WithValidDataAndDetails_ReturnsOk()
        {
            // Kiểm tra nhánh: Danh sách chi tiết không rỗng, SanPhamBienThe tồn tại, tạo phiếu nhập thành công
            // Arrange: Thêm biến thể sản phẩm và chuẩn bị dữ liệu phiếu nhập
            var sanPhamBienThe = new SanPhamBienThe { Id = 1, SoLuongTon = 10, Id_SanPham = 1, Id_Mau = 1, SizeId = 1 };
            await _context.SanPhamBienThes.AddAsync(sanPhamBienThe);
            await _context.SaveChangesAsync();
            var uploadPhieuNhap = new UploadPhieuNhapHang
            {
                NguoiLapPhieu = "user1",
                IdNhaCungCap = "1",
                TongTien = 100000,
                GhiChu = "Test",
                ChiTietPhieuNhaps = new List<UploadChiTietPhieuNhapHang>
                {
                    new UploadChiTietPhieuNhapHang { TenSanPhamBienThe = "Id: 1 Tên: Test", GiaNhapSanPhamBienThe = 50000, SoLuongNhap = 2 }
                }
            };

            // Act: Gọi phương thức PostTaoPhieuNhap
            var result = await _controller.PostTaoPhieuNhap(uploadPhieuNhap);

            // Assert: Kiểm tra tạo phiếu nhập, chi tiết, cập nhật tồn kho, và gọi SignalR
            Assert.IsType<OkResult>(result);
            var phieuNhap = await _context.PhieuNhapHangs.FirstOrDefaultAsync();
            Assert.NotNull(phieuNhap);
            Assert.Equal(100000, phieuNhap.TongTien);
            Assert.Single(_context.ChiTietPhieuNhapHangs);
            var updatedSanPhamBienThe = await _context.SanPhamBienThes.FindAsync(1);
            Assert.Equal(12, updatedSanPhamBienThe.SoLuongTon);
            _hubContextMock.Verify(h => h.Clients.All.BroadcastMessage(), Times.Once());
        }

        // TPNC17
        [Fact]
        public async Task PostTaoPhieuNhap_WithEmptyDetails_ReturnsOk()
        {
            // Kiểm tra nhánh: Danh sách chi tiết rỗng, tạo phiếu nhập thành công
            // Arrange: Chuẩn bị dữ liệu phiếu nhập không có chi tiết
            var uploadPhieuNhap = new UploadPhieuNhapHang
            {
                NguoiLapPhieu = "user1",
                IdNhaCungCap = "1",
                TongTien = 0,
                GhiChu = "Test",
                ChiTietPhieuNhaps = new List<UploadChiTietPhieuNhapHang>()
            };

            // Act: Gọi phương thức PostTaoPhieuNhap
            var result = await _controller.PostTaoPhieuNhap(uploadPhieuNhap);

            // Assert: Kiểm tra tạo phiếu nhập, không có chi tiết, và gọi SignalR
            Assert.IsType<OkResult>(result);
            var phieuNhap = await _context.PhieuNhapHangs.FirstOrDefaultAsync();
            Assert.NotNull(phieuNhap);
            Assert.Empty(_context.ChiTietPhieuNhapHangs);
            _hubContextMock.Verify(h => h.Clients.All.BroadcastMessage(), Times.Once());
        }

        // TPNC18
        [Fact]
        public async Task PostTaoPhieuNhap_WithNonExistentSanPhamBienThe_ReturnsOk()
        {
            // Kiểm tra nhánh: SanPhamBienThe không tồn tại, vẫn tạo phiếu nhập và chi tiết
            // Arrange: Chuẩn bị dữ liệu với ID biến thể không tồn tại
            var uploadPhieuNhap = new UploadPhieuNhapHang
            {
                NguoiLapPhieu = "user1",
                IdNhaCungCap = "1",
                TongTien = 100000,
                GhiChu = "Test",
                ChiTietPhieuNhaps = new List<UploadChiTietPhieuNhapHang>
                {
                    new UploadChiTietPhieuNhapHang { TenSanPhamBienThe = "Id: 999 Tên: Test", GiaNhapSanPhamBienThe = 50000, SoLuongNhap = 2 }
                }
            };

            // Act: Gọi phương thức PostTaoPhieuNhap
            var result = await _controller.PostTaoPhieuNhap(uploadPhieuNhap);

            // Assert: Kiểm tra tạo phiếu nhập, chi tiết, và gọi SignalR
            Assert.IsType<OkResult>(result);
            var phieuNhap = await _context.PhieuNhapHangs.FirstOrDefaultAsync();
            Assert.NotNull(phieuNhap);
            Assert.Single(_context.ChiTietPhieuNhapHangs);
            _hubContextMock.Verify(h => h.Clients.All.BroadcastMessage(), Times.Once());
        }

        // TPNC19
        [Fact]
        public async Task PostTaoPhieuNhap_WithInvalidIdNhaCungCap_ThrowsException()
        {
            // Kiểm tra nhánh: IdNhaCungCap không hợp lệ, ném FormatException
            // Arrange: Chuẩn bị dữ liệu với IdNhaCungCap không phải số
            var uploadPhieuNhap = new UploadPhieuNhapHang
            {
                NguoiLapPhieu = "user1",
                IdNhaCungCap = "invalid",
                TongTien = 100000,
                ChiTietPhieuNhaps = new List<UploadChiTietPhieuNhapHang>()
            };

            // Act & Assert: Kiểm tra ném FormatException
            await Assert.ThrowsAsync<FormatException>(() => _controller.PostTaoPhieuNhap(uploadPhieuNhap));
        }


        // TPNC20
        [Fact]
        public async Task PostTaoPhieuNhap_WithInvalidSanPhamBienTheId_ThrowsException()
        {
            // Kiểm tra nhánh: StringHelper.XuLyIdSPBT trả về ID không hợp lệ, ném FormatException
            // Arrange: Chuẩn bị dữ liệu với TenSanPhamBienThe không chứa ID hợp lệ
            var uploadPhieuNhap = new UploadPhieuNhapHang
            {
                NguoiLapPhieu = "user1",
                IdNhaCungCap = "1",
                TongTien = 100000,
                GhiChu = "Test",
                ChiTietPhieuNhaps = new List<UploadChiTietPhieuNhapHang>
                {
                    new UploadChiTietPhieuNhapHang { TenSanPhamBienThe = "InvalidFormat", GiaNhapSanPhamBienThe = 50000, SoLuongNhap = 2 }
                }
            };

            // Act & Assert: Kiểm tra ném FormatException (giả định XuLyIdSPBT ném lỗi)
            await Assert.ThrowsAsync<FormatException>(() => _controller.PostTaoPhieuNhap(uploadPhieuNhap));
        }


        // TPNC21
        [Fact]
        public async Task PostTaoPhieuNhap_WithDatabaseError_ThrowsException()
        {
            // Kiểm tra nhánh: Lỗi database khi lưu, ném DbUpdateException
            // Arrange: Sử dụng một context mock để giả lập lỗi SaveChangesAsync
            var mockContext = new Mock<DPContext>(new DbContextOptions<DPContext>());
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException());
            var errorController = new TaoPhieuNhapsController(mockContext.Object, _hubContextMock.Object);
            errorController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            var uploadPhieuNhap = new UploadPhieuNhapHang
            {
                NguoiLapPhieu = "user1",
                IdNhaCungCap = "1",
                TongTien = 0,
                ChiTietPhieuNhaps = new List<UploadChiTietPhieuNhapHang>()
            };

            // Act & Assert: Kiểm tra ném DbUpdateException
            await Assert.ThrowsAsync<DbUpdateException>(() => errorController.PostTaoPhieuNhap(uploadPhieuNhap));
        }

        // --- Tests for GetDetailPhieuNhapAsync ---
        // TPNC22
        [Fact]
        public async Task GetDetailPhieuNhapAsync_WithValidData_ReturnsDetail()
        {
            // Kiểm tra nhánh: Phiếu nhập tồn tại với chi tiết, trả về đối tượng đầy đủ
            // Arrange: Thêm dữ liệu đầy đủ cho phiếu nhập và chi tiết
            var nhaCungCap = new NhaCungCap { Id = 1, Ten = "NCC1", SDT = "123456789", DiaChi = "Hanoi" };
            var user = new AppUser { Id = "user1", FirstName = "John", LastName = "Doe" };
            var sanPham = new SanPham { Id = 1, Ten = "SP1", Id_Loai = 1, GiaNhap = 50000 };
            var loai = new Loai { Id = 1, Ten = "Loai1" };
            var mauSac = new MauSac { Id = 1, MaMau = "Red" };
            var size = new Size { Id = 1, TenSize = "M" };
            var sanPhamBienThe = new SanPhamBienThe { Id = 1, Id_SanPham = 1, Id_Mau = 1, SizeId = 1 };
            var phieuNhap = new PhieuNhapHang
            {
                Id = 1,
                Id_NhaCungCap = 1,
                NguoiLapPhieu = "user1",
                SoChungTu = "PN001",
                TongTien = 100000,
                NgayTao = DateTime.Now
            };
            var chiTietPhieuNhap = new ChiTietPhieuNhapHang
            {
                Id = 1,
                Id_PhieuNhapHang = 1,
                Id_SanPhamBienThe = 1,
                SoluongNhap = 2,
                ThanhTienNhap = 100000
            };
            await _context.NhaCungCaps.AddAsync(nhaCungCap);
            await _context.AppUsers.AddAsync(user);
            await _context.SanPhams.AddAsync(sanPham);
            await _context.Loais.AddAsync(loai);
            await _context.MauSacs.AddAsync(mauSac);
            await _context.Sizes.AddAsync(size);
            await _context.SanPhamBienThes.AddAsync(sanPhamBienThe);
            await _context.PhieuNhapHangs.AddAsync(phieuNhap);
            await _context.ChiTietPhieuNhapHangs.AddAsync(chiTietPhieuNhap);
            await _context.SaveChangesAsync();

            // Act: Gọi phương thức GetDetailPhieuNhapAsync
            var result = await _controller.GetDetailPhieuNhapAsync(1);

            // Assert: Kiểm tra trả về chi tiết phiếu nhập với thông tin đúng
            var actionResult = Assert.IsType<ActionResult<PhieuNhapChiTietPhieuNhap>>(result);
            var returnValue = Assert.IsType<PhieuNhapChiTietPhieuNhap>(actionResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Equal("NCC1", returnValue.NhaCungCap.Ten);
            Assert.Equal("John Doe", returnValue.NguoiLapPhieu);
            Assert.Single(returnValue.ChiTietPhieuNhaps);
            var chiTiet = returnValue.ChiTietPhieuNhaps.First();
            Assert.Equal("SP1 M Red", chiTiet.TenSanPhamBienTheMauSize);
        }

        // TPNC23
        [Fact]
        public async Task GetDetailPhieuNhapAsync_WithNonExistentId_ReturnsNotFound()
        {
            // Kiểm tra nhánh: Phiếu nhập không tồn tại, trả về NotFound
            // Arrange: Database rỗng
            // Act: Gọi phương thức GetDetailPhieuNhapAsync
            var result = await _controller.GetDetailPhieuNhapAsync(999);

            // Assert: Kiểm tra trả về NotFound
            Assert.IsType<NotFoundResult>(result.Result);
        }

        // TPNC24
        [Fact]
        public async Task GetDetailPhieuNhapAsync_WithNoDetails_ReturnsEmptyDetailList()
        {
            // Kiểm tra nhánh: Phiếu nhập tồn tại nhưng không có chi tiết, trả về danh sách chi tiết rỗng
            // Arrange: Thêm nhà cung cấp, người dùng, và phiếu nhập, không có chi tiết
            var nhaCungCap = new NhaCungCap { Id = 1, Ten = "NCC1", SDT = "123456789", DiaChi = "Hanoi" };
            var user = new AppUser { Id = "user1", FirstName = "John", LastName = "Doe" };
            var phieuNhap = new PhieuNhapHang
            {
                Id = 1,
                Id_NhaCungCap = 1,
                NguoiLapPhieu = "user1",
                SoChungTu = "PN001",
                TongTien = 100000,
                NgayTao = DateTime.Now
            };
            await _context.NhaCungCaps.AddAsync(nhaCungCap);
            await _context.AppUsers.AddAsync(user);
            await _context.PhieuNhapHangs.AddAsync(phieuNhap);
            await _context.SaveChangesAsync();

            // Act: Gọi phương thức GetDetailPhieuNhapAsync
            var result = await _controller.GetDetailPhieuNhapAsync(1);

            // Assert: Kiểm tra trả về phiếu nhập với danh sách chi tiết rỗng
            var actionResult = Assert.IsType<ActionResult<PhieuNhapChiTietPhieuNhap>>(result);
            var returnValue = Assert.IsType<PhieuNhapChiTietPhieuNhap>(actionResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Empty(returnValue.ChiTietPhieuNhaps);
        }


        // TPNC25
        [Fact]
        public async Task GetDetailPhieuNhapAsync_WithMissingNhaCungCap_ReturnsNotFound()
        {
            // Kiểm tra nhánh: Thiếu NhaCungCaps, LINQ join không khớp, trả về NotFound
            // Arrange: Thêm người dùng và phiếu nhập, thiếu nhà cung cấp
            var user = new AppUser { Id = "user1", FirstName = "John", LastName = "Doe" };
            var phieuNhap = new PhieuNhapHang
            {
                Id = 1,
                Id_NhaCungCap = 1,
                NguoiLapPhieu = "user1",
                SoChungTu = "PN001",
                TongTien = 100000
            };
            await _context.AppUsers.AddAsync(user);
            await _context.PhieuNhapHangs.AddAsync(phieuNhap);
            await _context.SaveChangesAsync();

            // Act: Gọi phương thức提高了GetDetailPhieuNhapAsync
            var result = await _controller.GetDetailPhieuNhapAsync(1);

            // Assert: Kiểm tra trả về NotFound do join thất bại
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }
}