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
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace API.Test
{
    public class ThongKeBieuDosControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly Mock<IDataConnector> _connectorMock;
        private readonly ThongKeBieuDosController _controller;

        public ThongKeBieuDosControllerTests() : base()
        {
            // Khởi tạo InMemoryDatabase
            _context = new DPContext(_options);

            // Giả lập SignalR HubContext
            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            // Giả lập IDataConnector
            _connectorMock = new Mock<IDataConnector>();

            // Khởi tạo controller
            _controller = new ThongKeBieuDosController(_context, _hubContext, _connectorMock.Object);

            // Làm sạch DB trước khi chạy mỗi bài kiểm thử
            Cleanup();
        }

        // Phương thức làm sạch DB
        private void Cleanup()
        {
            _context.HoaDons.RemoveRange(_context.HoaDons);
            _context.SaveChanges();
        }

        // TKB01: Kiểm tra lấy doanh số theo tháng trả về đúng khi có dữ liệu
        [Fact]
        public async Task GetDoanhSoThangasync_ShouldReturnAll_WhenDataExists()
        {
            // Arrange: Làm sạch DB và thêm 2 hóa đơn với tháng khác nhau
            Cleanup();
            var hoaDon1 = new HoaDon { NgayTao = new DateTime(2025, 1, 1), TongTien = 1000m, TrangThai = 2 };
            var hoaDon2 = new HoaDon { NgayTao = new DateTime(2025, 2, 1), TongTien = 2000m, TrangThai = 2 };
            _context.HoaDons.AddRange(hoaDon1, hoaDon2);
            await _context.SaveChangesAsync();

            // Kiểm tra DB trước khi gọi API
            Assert.Equal(2, await _context.HoaDons.CountAsync());

            // Act: Gọi API lấy doanh số theo tháng
            var result = await _controller.GetDoanhSoThangasync();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<ThangRevenue>>>(result);
            var list = Assert.IsType<List<ThangRevenue>>(actionResult.Value);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.Month == "1" && x.Revenues == 1000m);
            Assert.Contains(list, x => x.Month == "2" && x.Revenues == 2000m);

            // Kiểm tra trực tiếp DB sau khi gọi API
            Assert.Equal(2, await _context.HoaDons.CountAsync());
        }

        // TKB02: Kiểm tra lấy doanh số theo tháng trả về rỗng khi không có dữ liệu
        [Fact]
        public async Task GetDoanhSoThangasync_ShouldReturnEmpty_WhenNoData()
        {
            // Arrange: Làm sạch DB
            Cleanup();
            Assert.Equal(0, await _context.HoaDons.CountAsync());

            // Act: Gọi API lấy doanh số theo tháng
            var result = await _controller.GetDoanhSoThangasync();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<ThangRevenue>>>(result);
            var list = Assert.IsType<List<ThangRevenue>>(actionResult.Value);
            Assert.Empty(list);

            // Kiểm tra trực tiếp DB sau khi gọi API
            Assert.Equal(0, await _context.HoaDons.CountAsync());
        }

        // TKB03: Kiểm tra lấy doanh số theo ngày trong tháng trả về đúng khi có dữ liệu
        [Fact]
        public async Task GetDoanhSoNgayTheoThangasync_ShouldReturnAll_WhenDataExists()
        {
            // Arrange: Làm sạch DB và thêm 2 hóa đơn trong tháng 1
            Cleanup();
            var hoaDon1 = new HoaDon { NgayTao = new DateTime(2025, 1, 1), TongTien = 1000m, TrangThai = 2 };
            var hoaDon2 = new HoaDon { NgayTao = new DateTime(2025, 1, 2), TongTien = 2000m, TrangThai = 2 };
            _context.HoaDons.AddRange(hoaDon1, hoaDon2);
            await _context.SaveChangesAsync();

            // Kiểm tra DB trước khi gọi API
            Assert.Equal(2, await _context.HoaDons.CountAsync());

            // Act: Gọi API lấy doanh số theo ngày trong tháng 1
            var result = await _controller.GetDoanhSoNgayTheoThangasync("1");

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NgayRevenue>>>(result);
            var list = Assert.IsType<List<NgayRevenue>>(actionResult.Value);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.Ngay == new DateTime(2025, 1, 1) && x.Revenues == 1000m);
            Assert.Contains(list, x => x.Ngay == new DateTime(2025, 1, 2) && x.Revenues == 2000m);

            // Kiểm tra trực tiếp DB sau khi gọi API
            Assert.Equal(2, await _context.HoaDons.CountAsync());
        }

        // TKB04: Kiểm tra lấy doanh số theo ngày trong tháng trả về rỗng khi không có dữ liệu
        [Fact]
        public async Task GetDoanhSoNgayTheoThangasync_ShouldReturnEmpty_WhenNoData()
        {
            // Arrange: Làm sạch DB
            Cleanup();
            Assert.Equal(0, await _context.HoaDons.CountAsync());

            // Act: Gọi API lấy doanh số theo ngày trong tháng 1
            var result = await _controller.GetDoanhSoNgayTheoThangasync("1");

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NgayRevenue>>>(result);
            var list = Assert.IsType<List<NgayRevenue>>(actionResult.Value);
            Assert.Empty(list);

            // Kiểm tra trực tiếp DB sau khi gọi API
            Assert.Equal(0, await _context.HoaDons.CountAsync());
        }

        // TKB05: Kiểm tra lấy số lần xuất hiện trong đơn hàng trả về đúng khi có dữ liệu
        [Fact]
        public async Task GetSoLanXuatHienTrongDonHang_ShouldReturnAll_WhenDataExists()
        {
            // Arrange: Giả lập dữ liệu từ IDataConnector với thuộc tính TenSP và SoLanXuatHienTrongDonHang
            var data = new List<TenSPSoLanXuatHienTrongDonHang>
        {
            new TenSPSoLanXuatHienTrongDonHang { TenSP = "SP1", SoLanXuatHienTrongDonHang = 10 },
            new TenSPSoLanXuatHienTrongDonHang { TenSP = "SP2", SoLanXuatHienTrongDonHang = 20 }
        };
            _connectorMock.Setup(c => c.GetSoLanXuatHienTrongDonHang())
                .ReturnsAsync(data); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy số lần xuất hiện trong đơn hàng
            var result = await _controller.GetSoLanXuatHienTrongDonHang();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<TenSPSoLanXuatHienTrongDonHang>>>(result);
            var list = Assert.IsType<List<TenSPSoLanXuatHienTrongDonHang>>(actionResult.Value);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.TenSP == "SP1" && x.SoLanXuatHienTrongDonHang == 10);
            Assert.Contains(list, x => x.TenSP == "SP2" && x.SoLanXuatHienTrongDonHang == 20);
        }

        // TKB06: Kiểm tra lấy số lần xuất hiện trong đơn hàng trả về rỗng khi không có dữ liệu
        [Fact]
        public async Task GetSoLanXuatHienTrongDonHang_ShouldReturnEmpty_WhenNoData()
        {
            // Arrange: Giả lập dữ liệu rỗng từ IDataConnector
            var emptyData = new List<TenSPSoLanXuatHienTrongDonHang>();
            _connectorMock.Setup(c => c.GetSoLanXuatHienTrongDonHang())
                .ReturnsAsync(emptyData); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy số lần xuất hiện trong đơn hàng
            var result = await _controller.GetSoLanXuatHienTrongDonHang();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<TenSPSoLanXuatHienTrongDonHang>>>(result);
            var list = Assert.IsType<List<TenSPSoLanXuatHienTrongDonHang>>(actionResult.Value);
            Assert.Empty(list);
        }

        // TKB07: Kiểm tra top 10 sản phẩm lợi nhuận cao nhất trả về đúng khi có dữ liệu
        [Fact]
        public async Task Top10SanPhamLoiNhats_ShouldReturnAll_WhenDataExists()
        {
            // Arrange: Giả lập dữ liệu từ IDataConnector
            var data = new List<TenSanPhamDoanhSo>
        {
            new TenSanPhamDoanhSo { TenSP = "SP1", DoanhSoCaoNhat = 1000m },
            new TenSanPhamDoanhSo { TenSP = "SP2", DoanhSoCaoNhat = 2000m }
        };
            _connectorMock.Setup(c => c.Top10SanPhamLoiNhats())
                .ReturnsAsync(data); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy top 10 sản phẩm lợi nhuận cao nhất
            var result = await _controller.Top10SanPhamLoiNhats();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<TenSanPhamDoanhSo>>>(result);
            var list = Assert.IsType<List<TenSanPhamDoanhSo>>(actionResult.Value);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.TenSP == "SP1" && x.DoanhSoCaoNhat == 1000m);
            Assert.Contains(list, x => x.TenSP == "SP2" && x.DoanhSoCaoNhat == 2000m);
        }

        // TKB08: Kiểm tra top 10 sản phẩm lợi nhuận cao nhất trả về rỗng khi không có dữ liệu
        [Fact]
        public async Task Top10SanPhamLoiNhats_ShouldReturnEmpty_WhenNoData()
        {
            // Arrange: Giả lập dữ liệu rỗng từ IDataConnector
            var emptyData = new List<TenSanPhamDoanhSo>();
            _connectorMock.Setup(c => c.Top10SanPhamLoiNhats())
                .ReturnsAsync(emptyData); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy top 10 sản phẩm lợi nhuận cao nhất
            var result = await _controller.Top10SanPhamLoiNhats();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<TenSanPhamDoanhSo>>>(result);
            var list = Assert.IsType<List<TenSanPhamDoanhSo>>(actionResult.Value);
            Assert.Empty(list);
        }

        // TKB09: Kiểm tra thương hiệu bán chạy nhất trong năm 2021 trả về đúng khi có dữ liệu
        [Fact]
        public async Task GetNhanHieuBanChayNhatTrongNam2021_ShouldReturnAll_WhenDataExists()
        {
            // Arrange: Giả lập dữ liệu từ IDataConnector
            var data = new List<NhanHieuBanChayNhatTrongNam2021>
        {
            new NhanHieuBanChayNhatTrongNam2021 { Ten = "NH1", SoLuong = 10 },
            new NhanHieuBanChayNhatTrongNam2021 { Ten = "NH2", SoLuong = 20 }
        };
            _connectorMock.Setup(c => c.GetNhanHieuBanChayNhatTrongNam2021())
                .ReturnsAsync(data); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy thương hiệu bán chạy nhất trong năm 2021
            var result = await _controller.GetNhanHieuBanChayNhatTrongNam2021();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NhanHieuBanChayNhatTrongNam2021>>>(result);
            var list = Assert.IsType<List<NhanHieuBanChayNhatTrongNam2021>>(actionResult.Value);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.Ten == "NH1" && x.SoLuong == 10);
            Assert.Contains(list, x => x.Ten == "NH2" && x.SoLuong == 20);
        }

        // TKB10: Kiểm tra thương hiệu bán chạy nhất trong năm 2021 trả về rỗng khi không có dữ liệu
        [Fact]
        public async Task GetNhanHieuBanChayNhatTrongNam2021_ShouldReturnEmpty_WhenNoData()
        {
            // Arrange: Giả lập dữ liệu rỗng từ IDataConnector
            var emptyData = new List<NhanHieuBanChayNhatTrongNam2021>();
            _connectorMock.Setup(c => c.GetNhanHieuBanChayNhatTrongNam2021())
                .ReturnsAsync(emptyData); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy thương hiệu bán chạy nhất trong năm 2021
            var result = await _controller.GetNhanHieuBanChayNhatTrongNam2021();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NhanHieuBanChayNhatTrongNam2021>>>(result);
            var list = Assert.IsType<List<NhanHieuBanChayNhatTrongNam2021>>(actionResult.Value);
            Assert.Empty(list);
        }

        // TKB11: Kiểm tra biến thể đạt doanh thu cao nhất trả về đúng khi có dữ liệu
        [Fact]
        public async Task DataDataSetBanRaTonKho_ShouldReturnAll_WhenDataExists()
        {
            // Arrange: Giả lập dữ liệu từ IDataConnector
            var data = new List<DataSetBanRaTonKho>
        {
            new DataSetBanRaTonKho { Ten = "SPBT1", GiaTriBanRa = 1000m, GiaTriTonKho = 10 },
            new DataSetBanRaTonKho { Ten = "SPBT2", GiaTriBanRa = 2000m, GiaTriTonKho = 20 }
        };
            _connectorMock.Setup(c => c.DataDataSetBanRaTonKho())
                .ReturnsAsync(data); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy biến thể đạt doanh thu cao nhất
            var result = await _controller.DataDataSetBanRaTonKho();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<DataSetBanRaTonKho>>>(result);
            var list = Assert.IsType<List<DataSetBanRaTonKho>>(actionResult.Value);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.Ten == "SPBT1" && x.GiaTriBanRa == 1000m);
            Assert.Contains(list, x => x.Ten == "SPBT2" && x.GiaTriBanRa == 2000m);
        }

        // TKB12: Kiểm tra biến thể đạt doanh thu cao nhất trả về rỗng khi không có dữ liệu
        [Fact]
        public async Task DataDataSetBanRaTonKho_ShouldReturnEmpty_WhenNoData()
        {
            // Arrange: Giả lập dữ liệu rỗng từ IDataConnector
            var emptyData = new List<DataSetBanRaTonKho>();
            _connectorMock.Setup(c => c.DataDataSetBanRaTonKho())
                .ReturnsAsync(emptyData); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy biến thể đạt doanh thu cao nhất
            var result = await _controller.DataDataSetBanRaTonKho();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<DataSetBanRaTonKho>>>(result);
            var list = Assert.IsType<List<DataSetBanRaTonKho>>(actionResult.Value);
            Assert.Empty(list);
        }

        // TKB13: Kiểm tra doanh số theo nhà cung cấp trả về đúng khi có dữ liệu
        [Fact]
        public async Task GetDoanhSoBans_ShouldReturnAll_WhenDataExists()
        {
            // Arrange: Giả lập dữ liệu từ IDataConnector
            var data = new List<NhaCungCapTongTien>
        {
            new NhaCungCapTongTien { Ten = "NCC1", TongTien = 1000m },
            new NhaCungCapTongTien { Ten = "NCC2", TongTien = 2000m }
        };
            _connectorMock.Setup(c => c.GetDoanhSoBans())
                .ReturnsAsync(data); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy doanh số theo nhà cung cấp
            var result = await _controller.GetDoanhSoBans();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NhaCungCapTongTien>>>(result);
            var list = Assert.IsType<List<NhaCungCapTongTien>>(actionResult.Value);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.Ten == "NCC1" && x.TongTien == 1000m);
            Assert.Contains(list, x => x.Ten == "NCC2" && x.TongTien == 2000m);
        }

        // TKB14: Kiểm tra doanh số theo nhà cung cấp trả về rỗng khi không có dữ liệu
        [Fact]
        public async Task GetDoanhSoBans_ShouldReturnEmpty_WhenNoData()
        {
            // Arrange: Giả lập dữ liệu rỗng từ IDataConnector
            var emptyData = new List<NhaCungCapTongTien>();
            _connectorMock.Setup(c => c.GetDoanhSoBans())
                .ReturnsAsync(emptyData); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy doanh số theo nhà cung cấp
            var result = await _controller.GetDoanhSoBans();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NhaCungCapTongTien>>>(result);
            var list = Assert.IsType<List<NhaCungCapTongTien>>(actionResult.Value);
            Assert.Empty(list);
        }

        // TKB15: Kiểm tra số lượng theo nhà cung cấp trả về đúng khi có dữ liệu
        [Fact]
        public async Task GetNhaCungCapSoLuongs_ShouldReturnAll_WhenDataExists()
        {
            // Arrange: Giả lập dữ liệu từ IDataConnector
            var data = new List<NhaCungCapSoLuong>
        {
            new NhaCungCapSoLuong { Ten = "NCC1", SoLuong = 10 },
            new NhaCungCapSoLuong { Ten = "NCC2", SoLuong = 20 }
        };
            _connectorMock.Setup(c => c.GetNhaCungCapSoLuongs())
                .ReturnsAsync(data); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy số lượng theo nhà cung cấp
            var result = await _controller.GetNhaCungCapSoLuongs();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NhaCungCapSoLuong>>>(result);
            var list = Assert.IsType<List<NhaCungCapSoLuong>>(actionResult.Value);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.Ten == "NCC1" && x.SoLuong == 10);
            Assert.Contains(list, x => x.Ten == "NCC2" && x.SoLuong == 20);
        }

        // TKB16: Kiểm tra số lượng theo nhà cung cấp trả về rỗng khi không có dữ liệu
        [Fact]
        public async Task GetNhaCungCapSoLuongs_ShouldReturnEmpty_WhenNoData()
        {
            // Arrange: Giả lập dữ liệu rỗng từ IDataConnector
            var emptyData = new List<NhaCungCapSoLuong>();
            _connectorMock.Setup(c => c.GetNhaCungCapSoLuongs())
                .ReturnsAsync(emptyData); // Trả về List<T> thay vì ActionResult

            // Act: Gọi API lấy số lượng theo nhà cung cấp
            var result = await _controller.GetNhaCungCapSoLuongs();

            // Assert: Kiểm tra kết quả
            var actionResult = Assert.IsType<ActionResult<IEnumerable<NhaCungCapSoLuong>>>(result);
            var list = Assert.IsType<List<NhaCungCapSoLuong>>(actionResult.Value);
            Assert.Empty(list);
        }
    }
}
