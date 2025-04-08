using API.Controllers;
using API.Data;
using API.Helper.SignalR;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace API.Test {
    public class SanPhamTheoLoaiNhanHieuControllersTests : TestBase {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly SanPhamBienThesController _controller;

        public SanPhamTheoLoaiNhanHieuControllersTests() : base() {
            _context = new DPContext(_options);

            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            _controller = new SanPhamBienThesController(_context, _hubContext);
        }

        // Sptlnh01
        [Fact]
        public async Task GetCategory_ReturnsProducts_WhenIdLoaiExists() {
            var controller = new SanPhamTheoLoaiNhanHieuController(_context);

            var result = await controller.GetCategory(1);

            var okResult = Assert.IsType<ActionResult<IEnumerable<SanPham>>>(result);
            var products = Assert.IsAssignableFrom<IEnumerable<SanPham>>(okResult.Value);
            Assert.NotEmpty(products); // Id_Loai = 1 có 2 sản phẩm
        }

        // Sptlnh02
        [Fact]
        public async Task GetCategory_ReturnsEmpty_WhenIdLoaiDoesNotExist() {
            var controller = new SanPhamTheoLoaiNhanHieuController(_context);

            var result = await controller.GetCategory(999); // Id không tồn tại

            var okResult = Assert.IsType<ActionResult<IEnumerable<SanPham>>>(result);
            var products = Assert.IsAssignableFrom<IEnumerable<SanPham>>(okResult.Value);
            Assert.Empty(products);
        }

        // Sptlnh03
        [Fact]
        public async Task GetBrand_ReturnsProducts_WhenIdNhanHieuExists() {
            var controller = new SanPhamTheoLoaiNhanHieuController(_context);

            var result = await controller.GetBrand(1);

            var okResult = Assert.IsType<ActionResult<IEnumerable<SanPham>>>(result);
            var products = Assert.IsAssignableFrom<IEnumerable<SanPham>>(okResult.Value);
            Assert.NotEmpty(products); // Id_NhanHieu = 1 có 2 sản phẩm
        }

        // Sptlnh04
        [Fact]
        public async Task GetBrand_ReturnsEmpty_WhenIdNhanHieuDoesNotExist() {
            var controller = new SanPhamTheoLoaiNhanHieuController(_context);

            var result = await controller.GetBrand(888);

            var okResult = Assert.IsType<ActionResult<IEnumerable<SanPham>>>(result);
            var products = Assert.IsAssignableFrom<IEnumerable<SanPham>>(okResult.Value);
            Assert.Empty(products);
        }

    }
}
