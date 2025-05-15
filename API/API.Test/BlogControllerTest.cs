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
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace API.Tests.Controllers
{
    public class BlogsControllerTests : IDisposable
    {
        private readonly DPContext _context;
        private readonly DbContextOptions<DPContext> _options;
        private readonly Mock<IHubContext<BroadcastHub, IHubClient>> _mockHubContext;
        private readonly BlogsController _controller;
        private readonly Mock<IHubClients<IHubClient>> _mockClients;

        public BlogsControllerTests()
        {
            // Setup in-memory database
            _options = new DbContextOptionsBuilder<DPContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new DPContext(_options);

            // Setup mocks for SignalR hub
            _mockHubContext = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            _mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClient = new Mock<IHubClient>();

            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(mockClient.Object);
            mockClient.Setup(c => c.BroadcastMessage()).Returns(Task.CompletedTask);

            // Initialize controller with mocks
            _controller = new BlogsController(_context, _mockHubContext.Object);

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

            // Add test blogs
            var blogs = new List<Blog>
            {
                new Blog
                {
                    Id = 1,
                    TieuDe = "Blog tiêu đề 1",
                    NoiDung = "Nội dung blog 1",
                    FkAppUser_NguoiThem = "user123"
                },
                new Blog
                {
                    Id = 2,
                    TieuDe = "Blog tiêu đề 2",
                    NoiDung = "Nội dung blog 2",
                    FkAppUser_NguoiThem = "user123"
                }
            };
            _context.Blogs.AddRange(blogs);

            // Add test images
            var images = new List<ImageBlog>
            {
                new ImageBlog
                {
                    Id = 1,
                    ImageName = "blog1_image.jpg",
                    FkBlogId = 1
                },
                new ImageBlog
                {
                    Id = 2,
                    ImageName = "blog2_image.jpg",
                    FkBlogId = 2
                }
            };
            _context.ImageBlogs.AddRange(images);

            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region GetllBlogs Tests

        [Fact]
        public async Task TC_BLOG_01_GetllBlogs_ReturnsAllBlogs()
        {
            // Act
            var result = await _controller.GetllBlogs();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<BlogAndImage>>>(result);
            var blogList = Assert.IsAssignableFrom<List<BlogAndImage>>(actionResult.Value);

            Assert.Equal(2, blogList.Count);
            Assert.Equal("Blog tiêu đề 1", blogList[0].TieuDe);
            Assert.Equal("blog1_image.jpg", blogList[0].image);
            Assert.Equal("Nguyễn Văn A", blogList[0].nameUser);

            // Check DB
            var dbBlogCount = await _context.Blogs.CountAsync();
            Assert.Equal(2, dbBlogCount);
        }

        [Fact]
        public async Task TC_BLOG_02_GetllBlogs_ReturnsEmptyList_WhenNoBlogsExist()
        {
            // Arrange
            _context.Blogs.RemoveRange(_context.Blogs);
            _context.ImageBlogs.RemoveRange(_context.ImageBlogs);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetllBlogs();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<BlogAndImage>>>(result);
            var blogList = Assert.IsAssignableFrom<List<BlogAndImage>>(actionResult.Value);

            Assert.Empty(blogList);

            // Check DB
            var dbBlogCount = await _context.Blogs.CountAsync();
            Assert.Equal(0, dbBlogCount);
        }

        #endregion

        #region GetBlog Tests

        [Fact]
        public async Task TC_BLOG_03_GetBlog_ReturnsAllBlogs()
        {
            // Act
            var result = await _controller.GetBlog();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var blogs = Assert.IsAssignableFrom<List<dynamic>>(jsonResult.Value);

            Assert.Equal(2, blogs.Count);

            // Check DB
            var dbBlogCount = await _context.Blogs.CountAsync();
            Assert.Equal(2, dbBlogCount);
        }

        #endregion

        #region PutBlog Tests

        [Fact]
        public async Task TC_BLOG_04_PutBlog_UpdatesExistingBlog()
        {
            // Arrange
            var blogId = 1;

            // Tạo mock file
            var fileMock = new Mock<IFormFile>();
            var content = "Hello World from a Fake File";
            var fileName = "test.jpg";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.FileName).Returns(fileName);
            fileMock.Setup(_ => _.Length).Returns(ms.Length);

            var files = new List<IFormFile> { fileMock.Object };

            var updateModel = new UploadBlog
            {
                TieuDe = "Blog tiêu đề 1 đã cập nhật",
                NoiDung = "Nội dung blog 1 đã cập nhật",
                FkUserId = "user123",
                files = files
            };

            // Act
            var result = await _controller.PutBlog(blogId, updateModel);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Check DB
            var updatedBlog = await _context.Blogs.FindAsync(blogId);
            Assert.NotNull(updatedBlog);
            Assert.Equal("Blog tiêu đề 1 đã cập nhật", updatedBlog.TieuDe);
            Assert.Equal("Nội dung blog 1 đã cập nhật", updatedBlog.NoiDung);

            // Check notification
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "Blog tiêu đề 1 đã cập nhật");
            Assert.NotNull(notification);
            Assert.Equal("Edit", notification.TranType);
        }

        [Fact]
        public async Task TC_BLOG_05_PutBlog_ThrowsException_WhenBlogNotFound()
        {
            // Arrange
            var nonExistentBlogId = 999;

            var files = new List<IFormFile>();

            var updateModel = new UploadBlog
            {
                TieuDe = "Blog không tồn tại",
                NoiDung = "Nội dung blog không tồn tại",
                FkUserId = "user123",
                files = files
            };

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(() =>
                _controller.PutBlog(nonExistentBlogId, updateModel));

            // Check DB
            var blog = await _context.Blogs.FindAsync(nonExistentBlogId);
            Assert.Null(blog);
        }

        #endregion

        #region PostBlog Tests

        [Fact]
        public async Task TC_BLOG_06_PostBlog_CreatesNewBlog()
        {
            // Arrange
            // Tạo mock file
            var fileMock = new Mock<IFormFile>();
            var content = "Hello World from a Fake File";
            var fileName = "test.jpg";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.FileName).Returns(fileName);
            fileMock.Setup(_ => _.Length).Returns(ms.Length);

            var files = new List<IFormFile> { fileMock.Object };

            var newBlogModel = new UploadBlog
            {
                TieuDe = "Blog tiêu đề mới",
                NoiDung = "Nội dung blog mới",
                FkUserId = "user123",
                files = files
            };

            // Initial count
            var initialBlogCount = await _context.Blogs.CountAsync();

            // Act
            var result = await _controller.PostBlog(newBlogModel);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Check DB
            var currentBlogCount = await _context.Blogs.CountAsync();
            Assert.Equal(initialBlogCount + 1, currentBlogCount);

            var newBlog = await _context.Blogs.FirstOrDefaultAsync(b => b.TieuDe == "Blog tiêu đề mới");
            Assert.NotNull(newBlog);
            Assert.Equal("Nội dung blog mới", newBlog.NoiDung);
            Assert.Equal("user123", newBlog.FkAppUser_NguoiThem);

            // Check notification
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.TenSanPham == "Blog tiêu đề mới");
            Assert.NotNull(notification);
            Assert.Equal("Add", notification.TranType);
        }

        #endregion

        #region DeleteBlog Tests

        [Fact]
        public async Task TC_BLOG_07_DeleteBlog_RemovesBlog()
        {
            // Arrange
            var blogId = 2;
            var initialBlogCount = await _context.Blogs.CountAsync();
            var initialImageCount = await _context.ImageBlogs.CountAsync(i => i.FkBlogId == blogId);
            Assert.Equal(1, initialImageCount); // Verify we have an image to delete

            // Act
            var result = await _controller.DeleteBlog(blogId);

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            // Check DB
            var currentBlogCount = await _context.Blogs.CountAsync();
            Assert.Equal(initialBlogCount - 1, currentBlogCount);

            var deletedBlog = await _context.Blogs.FindAsync(blogId);
            Assert.Null(deletedBlog);

            var remainingImages = await _context.ImageBlogs.CountAsync(i => i.FkBlogId == blogId);
            Assert.Equal(0, remainingImages);
        }

        [Fact]
        public async Task TC_BLOG_08_DeleteBlog_ThrowsException_WhenBlogNotFound()
        {
            // Arrange
            var nonExistentBlogId = 999;

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(() =>
                _controller.DeleteBlog(nonExistentBlogId));

            // Check DB unchanged
            var initialBlogCount = await _context.Blogs.CountAsync();
            Assert.Equal(2, initialBlogCount);
        }

        #endregion
    }
}
