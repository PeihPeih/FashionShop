using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Controllers;
using API.Data;
using API.Dtos;
using API.Models;
using Microsoft.AspNetCore.Http;
using API.Helper.SignalR;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace API.Test
{
    public class CartsControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly CartsController _controller;

        public CartsControllerTests() : base()
        {
            _context = new DPContext(_options);
            _controller = new CartsController(_context);
        }

        // -------------------------------------------------------------------------
        // Test cho API: POST api/Carts/getCart/{id}
        // -------------------------------------------------------------------------

        // C01: Kiểm tra API trả về danh sách giỏ hàng cho UserID đã có dữ liệu.
        [Fact]
        public async Task GetCarts_ShouldReturnCartList_WhenUserIdExists()
        {
            var user = new AppUser
            {
                FirstName = "Minh",
                LastName = "Nguyễn"
            };
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            var userId = user.Id;

            var product = new SanPham { Ten = "Product A", GiaBan = 100000 };
            _context.SanPhams.Add(product);
            await _context.SaveChangesAsync();

            var cart = new Cart
            {
                UserID = userId,
                SanPhamId = product.Id,
                SoLuong = 2,
                Mau = "Đen",
                Size = "M",
                Id_SanPhamBienThe = 101
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            var result = await _controller.GetCarts(userId);

            var actionResult = Assert.IsType<ActionResult<IEnumerable<CartViewModel>>>(result);
            var cartList = Assert.IsAssignableFrom<List<CartViewModel>>(actionResult.Value);
            Assert.Single(cartList);
            Assert.Equal(userId, cartList.First().UserID);
        }

        // C02: Kiểm tra API trả về danh sách rỗng nếu UserID không có giỏ hàng
        [Fact]
        public async Task GetCarts_ShouldReturnEmptyList_WhenUserIdHasNoCart()
        {
            var userId = "unknown_user";
            var result = await _controller.GetCarts(userId);

            var actionResult = Assert.IsType<ActionResult<IEnumerable<CartViewModel>>>(result);
            var cartList = Assert.IsAssignableFrom<List<CartViewModel>>(actionResult.Value);
            Assert.Empty(cartList);
        }

        // -------------------------------------------------------------------------
        // Test cho API: POST api/Carts/update
        // -------------------------------------------------------------------------

        // C03: Khi cập nhật giỏ hàng với số lượng < 1, item nên bị xóa khỏi DB.
        [Fact]
        public async Task UpdateCarts_ShouldDeleteCart_WhenQuantityLessThanOne()
        {
            var user = new AppUser
            {
                FirstName = "Minh",
                LastName = "Nguyễn"
            };
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            var userId = user.Id;

            var product = new SanPham { Ten = "Test Product", GiaBan = 100000 };
            _context.SanPhams.Add(product);
            await _context.SaveChangesAsync();

            var cart = new Cart
            {
                UserID = userId,
                SanPhamId = product.Id,
                SoLuong = 2,
                Mau = "Đỏ",
                Size = "L",
                Id_SanPhamBienThe = 1
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
            var cartId = cart.CartID;

            cart.SoLuong = 0;
            var result = await _controller.UpdateCarts(cart);

            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.DoesNotContain(_context.Carts, c => c.CartID == cartId);
        }

        // -------------------------------------------------------------------------
        // Test cho API: POST api/Carts (Add Cart)
        // -------------------------------------------------------------------------

        // C04: Khi thêm cart mới (không trùng với cart đã có) thì item được thêm vào.
        [Fact]
        public async Task PostCart_ShouldAddNewCart_WhenCartNotExists()
        {
            var user = new AppUser
            {
                FirstName = "Minh",
                LastName = "Nguyễn"
            };
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            var sp = new SanPham { GiaBan = 200000 };
            _context.SanPhams.Add(sp);
            await _context.SaveChangesAsync();

            var newCart = new Cart
            {
                UserID = user.Id,
                SanPhamId = sp.Id,
                Mau = "Xanh",
                Size = "XL",
                SoLuong = 2,
                Id_SanPhamBienThe = 1
            };

            var result = await _controller.PostCart(newCart);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var addedCart = await _context.Carts.FirstOrDefaultAsync(c => c.UserID == user.Id && c.SanPhamId == sp.Id);
            Assert.NotNull(addedCart);
            Assert.Equal(2, addedCart.SoLuong);
        }

        // C05: Khi thêm cart với thông tin trùng với cart đã có, số lượng được cộng dồn.
        [Fact]
        public async Task PostCart_ShouldIncreaseQuantity_WhenCartItemAlreadyExists()
        {
            var user = new AppUser
            {
                FirstName = "Minh",
                LastName = "Nguyễn"
            };
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            var sp = new SanPham { GiaBan = 200000 };
            _context.SanPhams.Add(sp);
            await _context.SaveChangesAsync();

            var cart = new Cart
            {
                UserID = user.Id,
                SanPhamId = sp.Id,
                Mau = "Xanh",
                Size = "XL",
                SoLuong = 1,
                Id_SanPhamBienThe = 1
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            var additionalCart = new Cart
            {
                UserID = user.Id,
                SanPhamId = sp.Id,
                Mau = "Xanh",
                Size = "XL",
                SoLuong = 3,
                Id_SanPhamBienThe = 1
            };

            var result = await _controller.PostCart(additionalCart);

            var existing = await _context.Carts.FirstOrDefaultAsync(c => c.UserID == user.Id && c.SanPhamId == sp.Id);
            Assert.Equal(4, existing.SoLuong);
        }

        // -------------------------------------------------------------------------
        // Test cho API: GET api/Carts/{id} (Lấy cart theo CartID)
        // -------------------------------------------------------------------------

        // C06: Kiểm tra API GET /api/Carts/{id} trả về đúng cart khi ID tồn tại.
        [Fact]
        public async Task GetCart_ShouldReturnCart_WhenCartIdExists()
        {
            var cart = new Cart { UserID = "userXYZ", SanPhamId = 1 };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            var result = await _controller.GetCart(cart.CartID);

            var actionResult = Assert.IsType<ActionResult<Cart>>(result);
            Assert.Equal(cart.CartID, actionResult.Value.CartID);
        }

        // C07: Kiểm tra API GET /api/Carts/{id} trả về NotFound khi CartID không tồn tại.
        [Fact]
        public async Task GetCart_ShouldReturnNotFound_WhenCartIdDoesNotExist()
        {
            var result = await _controller.GetCart(999);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        // C08: Kiểm tra cập nhật cart thành công qua PUT khi CartID khớp.
        [Fact]
        public async Task PutCart_ShouldUpdateCart_WhenValid()
        {
            var cart = new Cart { SanPhamId = 1, SoLuong = 1 };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            var updateCart = new Cart { CartID = cart.CartID, SanPhamId = 1, SoLuong = 10 };

            var result = await _controller.PutCart(cart.CartID, updateCart);

            Assert.IsType<NoContentResult>(result);
            var updated = await _context.Carts.FindAsync(cart.CartID);
            Assert.Equal(10, updated.SoLuong);
        }

        // C09: Đảm bảo trả về BadRequest khi CartID trên URL không khớp với cart.CartID trong payload.
        [Fact]
        public async Task PutCart_ShouldReturnBadRequest_WhenIdMismatch()
        {
            var cart = new Cart { CartID = 50 };
            var result = await _controller.PutCart(51, cart);
            Assert.IsType<BadRequestResult>(result);
        }

        // -------------------------------------------------------------------------
        // Test cho API: POST api/Carts/delete (Xóa cart theo UserID và Id_SanPhamBienThe)
        // -------------------------------------------------------------------------

        // C10: Kiểm tra API delete trả về xóa cart thành công theo điều kiện.
        [Fact]
        public async Task Delete_ShouldRemoveCart_WhenCartExists()
        {
            var user = new AppUser
            {
                FirstName = "Minh",
                LastName = "Nguyễn"
            };
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            var cart = new Cart { UserID = user.Id, Id_SanPhamBienThe = 5, SanPhamId = 1 };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            var deleteInput = new DeleteCart { Id_sanpham = 5, User_ID = user.Id };
            var result = await _controller.Delete(deleteInput);

            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.DoesNotContain(_context.Carts, c => c.UserID == user.Id && c.Id_SanPhamBienThe == 5);
        }

        // -------------------------------------------------------------------------
        // Test cho API: POST api/Carts/coutcomment (Đếm số comment cho từng sản phẩm)
        // -------------------------------------------------------------------------

        // C11: Kiểm tra API coutcomment trả về số comment đúng cho mỗi sản phẩm.
        [Fact]
        public async Task CoutComment_ShouldReturnCorrectCounts()
        {
            var sp = new SanPham { Ten = "Product Count" };
            _context.SanPhams.Add(sp);
            await _context.SaveChangesAsync();

            _context.UserComments.AddRange(
                new UserComment { IdSanPham = sp.Id },
                new UserComment { IdSanPham = sp.Id }
            );
            await _context.SaveChangesAsync();

            var result = await _controller.CoutComment();

            var jsonResult = Assert.IsType<JsonResult>(result);
            var counts = Assert.IsType<List<CountComment>>(jsonResult.Value);
            var item = counts.FirstOrDefault(c => c.sanpham.Id == sp.Id);
            Assert.NotNull(item);
            Assert.Equal(2, item.socomment);
        }

        // -------------------------------------------------------------------------
        // Test cho API: GET api/Carts/getcouttotalqty (Tổng số lượng của giỏ hàng)
        // -------------------------------------------------------------------------

        // C12: Kiểm tra API tổng hợp số lượng giỏ hàng trả về tổng số đúng.
        [Fact]
        public async Task GetTotalQty_ShouldReturnSumOfQuantities()
        {
            _context.Carts.AddRange(
                new Cart { SoLuong = 2 },
                new Cart { SoLuong = 3 },
                new Cart { SoLuong = 5 }
            );
            await _context.SaveChangesAsync();

            var result = await _controller.GetTotalQty();

            Assert.Equal(10, result.totalQty);
        }
    }
}

