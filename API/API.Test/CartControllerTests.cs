using API.Controllers;
using API.Data;
using API.Dtos;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace API.Tests.Controllers
{
    public class CartsControllerTests : IDisposable
    {
        private readonly DPContext _context;
        private readonly DbContextOptions<DPContext> _options;
        private readonly CartsController _controller;

        public CartsControllerTests()
        {
            // Setup in-memory database
            _options = new DbContextOptionsBuilder<DPContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new DPContext(_options);

            // Initialize controller
            _controller = new CartsController(_context);

            // Setup test data
            SeedDatabase();
        }

        private void SeedDatabase()
        {
            // Add test user
            var user = new AppUser
            {
                Id = "user123",
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "Nguyễn",
                LastName = "Văn A"
            };
            _context.AppUsers.Add(user);

            // Add test products
            var products = new List<SanPham>
            {
                new SanPham
                {
                    Id = 1,
                    Ten = "Sản phẩm 1",
                    GiaBan = 100000,
                    KhuyenMai = 0
                },
                new SanPham
                {
                    Id = 2,
                    Ten = "Sản phẩm 2",
                    GiaBan = 200000,
                    KhuyenMai = 10 // 10% discount
                }
            };
            _context.SanPhams.AddRange(products);

            // Add test product images
            var productImages = new List<ImageSanPham>
            {
                new ImageSanPham
                {
                    Id = 1,
                    IdSanPham = 1,
                    ImageName = "product1.jpg"
                },
                new ImageSanPham
                {
                    Id = 2,
                    IdSanPham = 2,
                    ImageName = "product2.jpg"
                }
            };
            _context.ImageSanPhams.AddRange(productImages);

            // Add test comments - Cập nhật theo định nghĩa UserComment mới
            var comments = new List<UserComment>
            {
                new UserComment
                {
                    Id = 1,
                    IdUser = "user123",
                    IdSanPham = 1,
                    Content = "Sản phẩm rất tốt",
                    NgayComment = DateTime.Now.AddDays(-2)
                },
                new UserComment
                {
                    Id = 2,
                    IdUser = "user123",
                    IdSanPham = 1,
                    Content = "Giao hàng nhanh",
                    NgayComment = DateTime.Now.AddDays(-1)
                },
                new UserComment
                {
                    Id = 3,
                    IdUser = "user123",
                    IdSanPham = 2,
                    Content = "Giá cả hợp lý",
                    NgayComment = DateTime.Now
                }
            };
            _context.UserComments.AddRange(comments);

            // Add test carts
            var carts = new List<Cart>
            {
                new Cart
                {
                    CartID = 1,
                    UserID = "user123",
                    SanPhamId = 1,
                    Id_SanPhamBienThe = 101,
                    Mau = "Đỏ",
                    Size = "M",
                    SoLuong = 2,
                    Gia = 100000
                },
                new Cart
                {
                    CartID = 2,
                    UserID = "user123",
                    SanPhamId = 2,
                    Id_SanPhamBienThe = 202,
                    Mau = "Xanh",
                    Size = "L",
                    SoLuong = 1,
                    Gia = 200000
                },
                new Cart
                {
                    CartID = 3,
                    UserID = "user456",
                    SanPhamId = 1,
                    Id_SanPhamBienThe = 103,
                    Mau = "Trắng",
                    Size = "S",
                    SoLuong = 3,
                    Gia = 100000
                }
            };
            _context.Carts.AddRange(carts);

            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region GetCarts Tests

        [Fact]
        public async Task TC_CART_01_GetCarts_ReturnsUserCart()
        {
            // Arrange
            string userId = "user123";

            // Act
            var result = await _controller.GetCarts(userId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<CartViewModel>>>(result);
            var cartItems = Assert.IsAssignableFrom<List<CartViewModel>>(actionResult.Value);

            // Kiểm tra đúng số lượng items
            Assert.Equal(2, cartItems.Count);

            // Kiểm tra chi tiết item đầu tiên
            Assert.Equal(101, cartItems[0].IdSanPhamBienThe);
            Assert.Equal("Đỏ", cartItems[0].Mau);
            Assert.Equal("M", cartItems[0].Size);
            Assert.Equal(2, cartItems[0].SoLuong);

            // Kiểm tra chi tiết sản phẩm trong giỏ hàng
            Assert.Equal(1, cartItems[0].ProductDetail.Id);
            Assert.Equal("Sản phẩm 1", cartItems[0].ProductDetail.Ten);
            Assert.Equal(100000, cartItems[0].ProductDetail.GiaBan);
            Assert.Equal("product1.jpg", cartItems[0].ProductDetail.Image);

            // Kiểm tra DB
            var dbCartCount = await _context.Carts.CountAsync(c => c.UserID == userId);
            Assert.Equal(2, dbCartCount);
        }

        [Fact]
        public async Task TC_CART_02_GetCarts_ReturnsEmptyList_WhenUserHasNoCart()
        {
            // Arrange
            string userId = "nonexistentuser";

            // Act
            var result = await _controller.GetCarts(userId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<CartViewModel>>>(result);
            var cartItems = Assert.IsAssignableFrom<List<CartViewModel>>(actionResult.Value);

            Assert.Empty(cartItems);

            // Kiểm tra DB
            var dbCartCount = await _context.Carts.CountAsync(c => c.UserID == userId);
            Assert.Equal(0, dbCartCount);
        }

        #endregion

        #region CoutComment Tests

        [Fact]
        public async Task TC_CART_03_CoutComment_ReturnsCommentCounts()
        {
            // Act
            var result = await _controller.CoutComment();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var commentCounts = Assert.IsAssignableFrom<List<CountComment>>(jsonResult.Value);

            Assert.Equal(2, commentCounts.Count); // 2 sản phẩm

            // Kiểm tra sản phẩm 1 có 2 comments
            var product1Comments = commentCounts.FirstOrDefault(c => c.sanpham.Id == 1);
            Assert.NotNull(product1Comments);
            Assert.Equal(2, product1Comments.socomment);

            // Kiểm tra sản phẩm 2 có 1 comment
            var product2Comments = commentCounts.FirstOrDefault(c => c.sanpham.Id == 2);
            Assert.NotNull(product2Comments);
            Assert.Equal(1, product2Comments.socomment);

            // Kiểm tra DB - xác nhận tổng số comments
            var dbCommentCount = await _context.UserComments.CountAsync();
            Assert.Equal(3, dbCommentCount);
        }

        #endregion

        #region GetTotalQty Tests

        [Fact]
        public async Task TC_CART_04_GetTotalQty_ReturnsTotalQuantity()
        {
            // Act
            var result = await _controller.GetTotalQty();

            // Assert - Cập nhật theo định nghĩa TotalCart mới
            Assert.IsType<TotalCart>(result);
            Assert.Equal(6, result.totalQty); // 2 + 1 + 3 = 6

            // Kiểm tra DB
            var dbTotalQty = await _context.Carts.SumAsync(c => c.SoLuong);
            Assert.Equal(6, dbTotalQty);
        }

        #endregion

        #region UpdateCarts Tests

        [Fact]
        public async Task TC_CART_05_UpdateCarts_UpdatesQuantity()
        {
            // Arrange
            var cartToUpdate = new Cart
            {
                CartID = 1,
                UserID = "user123",
                SoLuong = 5 // Update quantity from 2 to 5
            };

            // Act
            var result = await _controller.UpdateCarts(cartToUpdate);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var updatedCarts = Assert.IsAssignableFrom<List<CartViewModel>>(jsonResult.Value);

            // Kiểm tra số lượng items trong response
            Assert.Equal(2, updatedCarts.Count);

            // Kiểm tra item đã được cập nhật
            var updatedItem = updatedCarts.FirstOrDefault(c => c.CartID == 1);
            Assert.NotNull(updatedItem);
            Assert.Equal(5, updatedItem.SoLuong);

            // Kiểm tra DB
            var dbCart = await _context.Carts.FindAsync(1);
            Assert.NotNull(dbCart);
            Assert.Equal(5, dbCart.SoLuong);
        }

        [Fact]
        public async Task TC_CART_06_UpdateCarts_RemovesItem_WhenQuantityIsZero()
        {
            // Arrange
            var cartToUpdate = new Cart
            {
                CartID = 1,
                UserID = "user123",
                SoLuong = 0 // Update quantity to 0 should remove the item
            };

            // Act
            var result = await _controller.UpdateCarts(cartToUpdate);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var updatedCarts = Assert.IsAssignableFrom<List<CartViewModel>>(jsonResult.Value);

            // Kiểm tra số lượng items trong response
            Assert.Single(updatedCarts); // Should be only 1 item left

            // Kiểm tra item đã bị xóa
            var deletedItem = updatedCarts.FirstOrDefault(c => c.CartID == 1);
            Assert.Null(deletedItem);

            // Kiểm tra DB
            var dbCart = await _context.Carts.FindAsync(1);
            Assert.Null(dbCart);
        }

        #endregion

        #region GetCart Tests

        [Fact]
        public async Task TC_CART_07_GetCart_ReturnsCart()
        {
            // Arrange
            int cartId = 1;

            // Act
            var result = await _controller.GetCart(cartId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Cart>>(result);
            var cart = Assert.IsType<Cart>(actionResult.Value);

            Assert.Equal(cartId, cart.CartID);
            Assert.Equal("user123", cart.UserID);
            Assert.Equal(1, cart.SanPhamId);
            Assert.Equal(101, cart.Id_SanPhamBienThe);
            Assert.Equal("Đỏ", cart.Mau);
            Assert.Equal("M", cart.Size);
            Assert.Equal(2, cart.SoLuong);

            // Kiểm tra DB
            var dbCart = await _context.Carts.FindAsync(cartId);
            Assert.NotNull(dbCart);
            Assert.Equal("user123", dbCart.UserID);
        }

        [Fact]
        public async Task TC_CART_08_GetCart_ReturnsNotFound_WhenCartNotExist()
        {
            // Arrange
            int nonExistentCartId = 999;

            // Act
            var result = await _controller.GetCart(nonExistentCartId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Cart>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);

            // Kiểm tra DB
            var dbCart = await _context.Carts.FindAsync(nonExistentCartId);
            Assert.Null(dbCart);
        }

        #endregion

        #region PutCart Tests

        [Fact]
        public async Task TC_CART_09_PutCart_UpdatesCart()
        {
            // Arrange
            int cartId = 1;
            var updatedCart = new Cart
            {
                CartID = cartId,
                UserID = "user123",
                SanPhamId = 1,
                Id_SanPhamBienThe = 101,
                Mau = "Xanh lá", // Changed from "Đỏ" to "Xanh lá"
                Size = "L", // Changed from "M" to "L"
                SoLuong = 3, // Changed from 2 to 3
                Gia = 100000
            };

            // Act
            var result = await _controller.PutCart(cartId, updatedCart);

            // Assert
            Assert.IsType<NoContentResult>(result);

            // Kiểm tra DB
            var dbCart = await _context.Carts.FindAsync(cartId);
            Assert.NotNull(dbCart);
            Assert.Equal("Xanh lá", dbCart.Mau);
            Assert.Equal("L", dbCart.Size);
            Assert.Equal(3, dbCart.SoLuong);
        }

        [Fact]
        public async Task TC_CART_10_PutCart_ReturnsBadRequest_WhenIdMismatch()
        {
            // Arrange
            int cartId = 1;
            var cart = new Cart
            {
                CartID = 2, // Intentional mismatch with route id
                UserID = "user123",
                SanPhamId = 1,
                Mau = "Đỏ",
                Size = "M",
                SoLuong = 2,
                Gia = 100000
            };

            // Act
            var result = await _controller.PutCart(cartId, cart);

            // Assert
            Assert.IsType<BadRequestResult>(result);

            // Kiểm tra DB - không có thay đổi
            var dbCart = await _context.Carts.FindAsync(cartId);
            Assert.NotNull(dbCart);
            Assert.Equal("Đỏ", dbCart.Mau); // Unchanged
        }

        [Fact]
        public async Task TC_CART_11_PutCart_ReturnsNotFound_WhenCartNotExist()
        {
            // Arrange
            int nonExistentCartId = 999;
            var cart = new Cart
            {
                CartID = nonExistentCartId,
                UserID = "user123",
                SanPhamId = 1,
                Mau = "Đỏ",
                Size = "M",
                SoLuong = 2,
                Gia = 100000
            };

            // Act & Assert
            // Chủ động xóa entity trước khi test để tạo ra DbUpdateConcurrencyException
            var dbContext = new DPContext(_options);
            var controller = new CartsController(dbContext);

            // Act
            var result = await controller.PutCart(nonExistentCartId, cart);

            // Assert
            Assert.IsType<NotFoundResult>(result);

            // Kiểm tra DB
            var dbCart = await _context.Carts.FindAsync(nonExistentCartId);
            Assert.Null(dbCart);
        }

        #endregion

        #region Delete Tests

        [Fact]
        public async Task TC_CART_12_Delete_RemovesCartItem()
        {
            // Arrange
            var deleteModel = new DeleteCart
            {
                Id_sanpham = 101, // Id_SanPhamBienThe of the first cart item
                User_ID = "user123"
            };

            // Act
            var result = await _controller.Delete(deleteModel);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal("1", jsonResult.Value);

            // Kiểm tra DB
            var dbCart = await _context.Carts.FirstOrDefaultAsync(c =>
                c.Id_SanPhamBienThe == deleteModel.Id_sanpham && c.UserID == deleteModel.User_ID);
            Assert.Null(dbCart);
        }

        #endregion

        #region PostCart Tests

        [Fact]
        public async Task TC_CART_13_PostCart_AddsNewItem()
        {
            // Arrange
            var newCart = new Cart
            {
                UserID = "user123",
                SanPhamId = 2,
                Id_SanPhamBienThe = 203, // New variant
                Mau = "Đen", // New color
                Size = "XL", // New size
                SoLuong = 2
                // Gia will be set based on SanPhamId
            };

            // Initial count
            var initialCount = await _context.Carts.CountAsync();

            // Act
            var result = await _controller.PostCart(newCart);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Cart>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var returnValue = Assert.IsType<Cart>(createdAtActionResult.Value);
            Assert.Equal(newCart.UserID, returnValue.UserID);
            Assert.Equal(newCart.SanPhamId, returnValue.SanPhamId);
            Assert.Equal(newCart.Id_SanPhamBienThe, returnValue.Id_SanPhamBienThe);
            Assert.Equal(newCart.Mau, returnValue.Mau);
            Assert.Equal(newCart.Size, returnValue.Size);
            Assert.Equal(newCart.SoLuong, returnValue.SoLuong);

            // Kiểm tra Gia được set đúng
            Assert.Equal(200000, returnValue.Gia); // Giá của sản phẩm id=2

            // Kiểm tra DB
            var currentCount = await _context.Carts.CountAsync();
            Assert.Equal(initialCount + 1, currentCount);

            var dbCart = await _context.Carts.FirstOrDefaultAsync(c =>
                c.SanPhamId == newCart.SanPhamId &&
                c.Id_SanPhamBienThe == newCart.Id_SanPhamBienThe &&
                c.UserID == newCart.UserID);
            Assert.NotNull(dbCart);
            Assert.Equal("Đen", dbCart.Mau);
            Assert.Equal("XL", dbCart.Size);
            Assert.Equal(2, dbCart.SoLuong);
        }

        [Fact]
        public async Task TC_CART_14_PostCart_UpdatesExistingItem()
        {
            // Arrange - existing item in cart with same product, color, size
            var existingCart = new Cart
            {
                UserID = "user123",
                SanPhamId = 1,
                Id_SanPhamBienThe = 101,
                Mau = "Đỏ",
                Size = "M",
                SoLuong = 3 // Adding 3 more items
            };

            // Initial quantity
            var initialCart = await _context.Carts.FindAsync(1);
            var initialQuantity = initialCart.SoLuong; // Should be 2

            // Act
            var result = await _controller.PostCart(existingCart);

            // Assert
            var actionResult = Assert.IsType<ActionResult<Cart>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);

            // Kiểm tra DB
            var dbCart = await _context.Carts.FindAsync(1);
            Assert.NotNull(dbCart);
            Assert.Equal(initialQuantity + existingCart.SoLuong, dbCart.SoLuong); // 2 + 3 = 5
            Assert.Equal(5, dbCart.SoLuong);
        }

        #endregion
    }
}
