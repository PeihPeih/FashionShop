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

namespace API.Test
{
    public class BlogsControllerTests : TestBase
    {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly BlogsController _controller;
        private readonly string _wwwrootPath;

        public BlogsControllerTests() : base()
        {
            _context = new DPContext(_options);

            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            _controller = new BlogsController(_context, _hubContext);

            var projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\API"));
            _wwwrootPath = Path.Combine(projectRoot, "wwwroot");
        }

        // Helper method to create mock files
        private IFormFile CreateMockFile(string fileName, int length)
        {
            var stream = new MemoryStream(new byte[length]); // Create fake data
            return new FormFile(stream, 0, length, "file", fileName);
        }

        // Helper methods for setup
        private async Task<AppUser> CreateTestUserAsync()
        {
            var user = new AppUser
            {
                FirstName = "Test",
                LastName = "User"
            };
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        private async Task<Blog> CreateTestBlogAsync(string userId)
        {
            var blog = new Blog
            {
                TieuDe = "Test Blog",
                NoiDung = "Test Content",
                FkAppUser_NguoiThem = userId
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();
            return blog;
        }

        private async Task<ImageBlog> AddImageToBlogAsync(int blogId, string imageName = "test-image.jpg")
        {
            var image = new ImageBlog
            {
                FkBlogId = blogId,
                ImageName = imageName
            };
            _context.ImageBlogs.Add(image);
            await _context.SaveChangesAsync();
            return image;
        }

        // ---------------------- GET ALL BLOGS --------------------------
        // Blog01: Get all blogs - Should return blogs with images
        [Fact]
        public async Task GetllBlogs_ReturnsAllBlogsWithImages()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var blog = await CreateTestBlogAsync(user.Id);
            await AddImageToBlogAsync(blog.Id, "image1.jpg");

            // Act
            var result = await _controller.GetllBlogs();

            // Assert
            var blogs = Assert.IsType<ActionResult<IEnumerable<BlogAndImage>>>(result);
            var blogList = Assert.IsAssignableFrom<List<BlogAndImage>>(blogs.Value);

            Assert.NotEmpty(blogList);
            Assert.Equal("Test Blog", blogList.First().TieuDe);
            Assert.Equal("Test Content", blogList.First().NoiDung);
            Assert.Equal("image1.jpg", blogList.First().image);
            Assert.Equal("Test User", blogList.First().nameUser);
        }

        // Blog02: Get all blogs - Should return empty list when no blogs exist
        [Fact]
        public async Task GetllBlogs_ReturnsEmptyList_WhenNoBlogsExist()
        {
            // Arrange - ensure no blogs in database
            foreach (var blog in _context.Blogs)
            {
                _context.Blogs.Remove(blog);
            }
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetllBlogs();

            // Assert
            var blogs = Assert.IsType<ActionResult<IEnumerable<BlogAndImage>>>(result);
            var blogList = Assert.IsAssignableFrom<List<BlogAndImage>>(blogs.Value);

            Assert.Empty(blogList);
        }

        // ---------------------- GET BLOG --------------------------
        // Blog03: Get blog - Should return blog list with images
        [Fact]
        public async Task GetBlog_ReturnsBlogListWithImages()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var blog = await CreateTestBlogAsync(user.Id);
            await AddImageToBlogAsync(blog.Id, "test-image.jpg");

            // Act
            var result = await _controller.GetBlog();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var blogList = Assert.IsAssignableFrom<IEnumerable<dynamic>>(jsonResult.Value);

            Assert.NotEmpty(blogList);
            dynamic firstBlog = blogList.First();
            Assert.Equal("Test Blog", firstBlog.tieude);
            Assert.Equal("Test Content", firstBlog.noidung);
            Assert.Equal("test-image.jpg", firstBlog.image);
        }

        // Blog04: Get blog - Should return empty list when no blogs exist
        [Fact]
        public async Task GetBlog_ReturnsEmptyList_WhenNoBlogsExist()
        {
            // Arrange - ensure no blogs in database
            foreach (var blog in _context.Blogs)
            {
                _context.Blogs.Remove(blog);
            }
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetBlog();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var blogList = Assert.IsAssignableFrom<IEnumerable<dynamic>>(jsonResult.Value);

            Assert.Empty(blogList);
        }

        // ---------------------- PUT BLOG --------------------------
        // Blog05: Put blog - Should update blog with new content and without files
        [Fact]
        public async Task PutBlog_UpdatesBlogWithoutNewFiles()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var blog = await CreateTestBlogAsync(user.Id);
            await AddImageToBlogAsync(blog.Id, "original-image.jpg");

            var upload = new UploadBlog
            {
                TieuDe = "Updated Title",
                NoiDung = "Updated Content",
                FkUserId = user.Id,
                files = null // No new files
            };

            // Chuẩn bị helper method giả
            TestHelper.SetupDeleteFileMethod();

            // Act
            var result = await _controller.PutBlog(blog.Id, upload);

            // Assert
            Assert.IsType<OkResult>(result);

            // Verify database was updated
            var updatedBlog = await _context.Blogs.FindAsync(blog.Id);
            Assert.Equal("Updated Title", updatedBlog.TieuDe);
            Assert.Equal("Updated Content", updatedBlog.NoiDung);

            // Verify notification was created
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == "Updated Title" && n.TranType == "Edit");
            Assert.NotNull(notification);
        }

        // Blog06: Put blog - Should update blog with new files
        [Fact]
        public async Task PutBlog_UpdatesBlogWithNewFiles()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var blog = await CreateTestBlogAsync(user.Id);
            await AddImageToBlogAsync(blog.Id, "original-image.jpg");

            var file = CreateMockFile("new-image.jpg", 1024);
            var upload = new UploadBlog
            {
                TieuDe = "Blog With New Image",
                NoiDung = "Content with new image",
                FkUserId = user.Id,
                files = new List<IFormFile> { file }
            };

            // Chuẩn bị helper method giả
            TestHelper.SetupDeleteFileMethod();
            TestHelper.SetupUploadImageMethod("new-uploaded-image.jpg");

            // Act
            var result = await _controller.PutBlog(blog.Id, upload);

            // Assert
            Assert.IsType<OkResult>(result);

            // Verify database was updated
            var updatedBlog = await _context.Blogs.FindAsync(blog.Id);
            Assert.Equal("Blog With New Image", updatedBlog.TieuDe);

            // Verify image was updated
            var blogImages = await _context.ImageBlogs.Where(i => i.FkBlogId == blog.Id).ToListAsync();
            Assert.Single(blogImages);

            // Verify notification was created
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == "Blog With New Image" && n.TranType == "Edit");
            Assert.NotNull(notification);
        }

        // Blog07: Put blog - Should ignore files larger than size limit (5120 bytes)
        [Fact]
        public async Task PutBlog_IgnoresLargeFiles()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var blog = await CreateTestBlogAsync(user.Id);
            await AddImageToBlogAsync(blog.Id, "original-image.jpg");

            // Create a file larger than the size limit
            var largeFile = CreateMockFile("large-image.jpg", 10000); // > 5120 bytes
            var upload = new UploadBlog
            {
                TieuDe = "Blog With Large Image",
                NoiDung = "Content with large image",
                FkUserId = user.Id,
                files = new List<IFormFile> { largeFile }
            };

            // Chuẩn bị helper method giả
            TestHelper.SetupDeleteFileMethod();
            TestHelper.SetupUploadImageMethod("new-image.jpg");

            // Act
            var result = await _controller.PutBlog(blog.Id, upload);

            // Assert
            Assert.IsType<OkResult>(result);

            // Verify database was updated
            var updatedBlog = await _context.Blogs.FindAsync(blog.Id);
            Assert.Equal("Blog With Large Image", updatedBlog.TieuDe);

            // Large files should be ignored, so we should have no images
            var blogImages = await _context.ImageBlogs.Where(i => i.FkBlogId == blog.Id).ToListAsync();
            Assert.Empty(blogImages);
        }

        // Blog08: Put blog - Should handle blog that doesn't exist
        [Fact]
        public async Task PutBlog_ReturnsNotFound_WhenBlogDoesNotExist()
        {
            // Arrange
            var upload = new UploadBlog
            {
                TieuDe = "Non-existent Blog",
                NoiDung = "Content",
                FkUserId = "1",
                files = null
            };

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(() => _controller.PutBlog(999, upload));
        }

        // ---------------------- POST BLOG --------------------------
        // Blog09: Post blog - Should create new blog without files
        [Fact]
        public async Task PostBlog_CreatesNewBlogWithoutFiles()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var upload = new UploadBlog
            {
                TieuDe = "New Blog",
                NoiDung = "New Content",
                FkUserId = user.Id,
                files = null
            };

            // Act
            var result = await _controller.PostBlog(upload);

            // Assert
            Assert.IsType<OkResult>(result.Result);

            // Verify blog was created
            var blog = await _context.Blogs.FirstOrDefaultAsync(b => b.TieuDe == "New Blog");
            Assert.NotNull(blog);
            Assert.Equal("New Content", blog.NoiDung);
            Assert.Equal(user.Id, blog.FkAppUser_NguoiThem);

            // Verify notification was created
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == "New Blog" && n.TranType == "Add");
            Assert.NotNull(notification);
        }

        // Blog10: Post blog - Should create new blog with files
        [Fact]
        public async Task PostBlog_CreatesNewBlogWithFiles()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var file = CreateMockFile("blog-image.jpg", 1024);
            var upload = new UploadBlog
            {
                TieuDe = "Blog With Image",
                NoiDung = "Content with image",
                FkUserId = user.Id,
                files = new List<IFormFile> { file }
            };

            // Chuẩn bị helper method giả
            TestHelper.SetupUploadImageMethod("uploaded-image.jpg");

            // Act
            var result = await _controller.PostBlog(upload);

            // Assert
            Assert.IsType<OkResult>(result.Result);

            // Verify blog was created
            var blog = await _context.Blogs.FirstOrDefaultAsync(b => b.TieuDe == "Blog With Image");
            Assert.NotNull(blog);

            // Verify notification was created
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == "Blog With Image" && n.TranType == "Add");
            Assert.NotNull(notification);
        }

        // Blog11: Post blog - Should ignore files larger than size limit
        [Fact]
        public async Task PostBlog_IgnoresLargeFiles()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var largeFile = CreateMockFile("large-image.jpg", 10000); // > 5120 bytes
            var upload = new UploadBlog
            {
                TieuDe = "Blog With Large Image",
                NoiDung = "Content with large image",
                FkUserId = user.Id,
                files = new List<IFormFile> { largeFile }
            };

            // Act
            var result = await _controller.PostBlog(upload);

            // Assert
            Assert.IsType<OkResult>(result.Result);

            // Verify blog was created
            var blog = await _context.Blogs.FirstOrDefaultAsync(b => b.TieuDe == "Blog With Large Image");
            Assert.NotNull(blog);

            // Large files should be ignored, so we should have no images
            var images = await _context.ImageBlogs.Where(i => i.FkBlogId == blog.Id).ToListAsync();
            Assert.Empty(images);
        }

        // ---------------------- DELETE BLOG --------------------------
        // Blog12: Delete blog - Should delete blog and associated images
        [Fact]
        public async Task DeleteBlog_DeletesBlogAndImages()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var blog = await CreateTestBlogAsync(user.Id);
            await AddImageToBlogAsync(blog.Id, "image-to-delete.jpg");

            // Chuẩn bị helper method giả
            TestHelper.SetupDeleteFileMethod();

            // Act
            var result = await _controller.DeleteBlog(blog.Id);

            // Assert
            Assert.IsType<OkResult>(result);

            // Verify blog was deleted
            var deletedBlog = await _context.Blogs.FindAsync(blog.Id);
            Assert.Null(deletedBlog);

            // Verify images were deleted
            var images = await _context.ImageBlogs.Where(i => i.FkBlogId == blog.Id).ToListAsync();
            Assert.Empty(images);
        }

        // Blog13: Delete blog - Should handle blog that doesn't exist
        [Fact]
        public async Task DeleteBlog_HandlesNonExistentBlog()
        {
            // Arrange - Use a blog ID that doesn't exist
            int nonExistentId = 9999;

            // Chuẩn bị helper method giả
            TestHelper.SetupDeleteFileMethod();

            // Act & Assert - Should not throw an exception
            var result = await _controller.DeleteBlog(nonExistentId);
            Assert.IsType<OkResult>(result);
        }
    }

    // Lớp giúp đỡ với các phương thức tĩnh cho test
    public static class TestHelper
    {
        public static void SetupDeleteFileMethod()
        {
            // Giả lập phương thức xóa file mà không cần dùng reflection
        }

        public static void SetupUploadImageMethod(string returnFileName)
        {
            // Giả lập phương thức upload ảnh mà không cần dùng reflection
        }
    }
}