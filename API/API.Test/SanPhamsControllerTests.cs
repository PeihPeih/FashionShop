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
using Nancy.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static Syncfusion.XlsIO.Parser.Biff_Records.Charts.ChartAlrunsRecord;

namespace API.Test {
    public class SanPhamsControllerTests : TestBase {
        private readonly DPContext _context;
        private readonly IHubContext<BroadcastHub, IHubClient> _hubContext;
        private readonly SanPhamsController _controller;
        private readonly string _wwwrootPath;


        public SanPhamsControllerTests() : base() {
            _context = new DPContext(_options);

            var mockHub = new Mock<IHubContext<BroadcastHub, IHubClient>>();
            var mockClients = new Mock<IHubClients<IHubClient>>();
            var mockClientProxy = new Mock<IHubClient>();
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
            _hubContext = mockHub.Object;

            _controller = new SanPhamsController(_context, _hubContext);

            var projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\API"));
            _wwwrootPath = Path.Combine(projectRoot, "wwwroot");
        }

        private List<IFormFile> GenerateFakeFiles(int count, int length) {
            var files = new List<IFormFile>();
            for (int i = 0; i < count; i++) {
                var mockFile = new Mock<IFormFile>();
                mockFile.Setup(f => f.Length).Returns(length);
                mockFile.Setup(f => f.FileName).Returns($"image_{i}.jpg");
                mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[length]));
                files.Add(mockFile.Object);
            }
            return files;
        }

        private IFormFile CreateMockFile(string fileName, int length) {
            var stream = new MemoryStream(new byte[length]); // tạo dữ liệu giả
            return new FormFile(stream, 0, length, "file", fileName);
        }

        // NOTE: API Size và Mau k test vì k liên quan

        // ---------------------- LIKE SAN PHAM -----------------------
        // Sp01: Thêm like khi chưa có
        [Fact]
        public async Task LikeSanPham_AddsLike_WhenNotExist() {
            // Arrange
            var userlike = new UserLike {
                IdUser = 1.ToString(),
                IdSanPham = 1
            };

            // Act
            var result = await _controller.LikeSanPham(userlike);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(1, jsonResult.Value); // Đã thêm like

            // Kiểm tra trong database đã có 1 bản ghi like
            var stored = await _context.UserLikes
                .FirstOrDefaultAsync(u => u.IdUser == userlike.IdUser && u.IdSanPham == userlike.IdSanPham);
            Assert.NotNull(stored);
        }

        // Sp02: Bỏ like khi đã like
        [Fact]
        public async Task LikeSanPham_RemovesLike_WhenAlreadyExists() {
            // Arrange
            var userlike = new UserLike {
                IdUser = 2.ToString(),
                IdSanPham = 2
            };

            // Thêm like trước
            _context.UserLikes.Add(new UserLike {
                IdUser = userlike.IdUser,
                IdSanPham = userlike.IdSanPham
            });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.LikeSanPham(userlike);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(2, jsonResult.Value); // Đã unlike

            // Kiểm tra không còn like trong DB
            var stored = await _context.UserLikes
                .FirstOrDefaultAsync(u => u.IdUser == userlike.IdUser && u.IdSanPham == userlike.IdSanPham);
            Assert.Null(stored);
        }

        // ----------------------- LIST LIKE SAN PHAM --------------------------
        // Sp03: K có like -> trả ra mảng rỗng
        [Fact]
        public async Task ListLikeSanPham_ReturnsEmptyList_WhenUserHasNoLikes() {
            // Arrange
            var userlike = new UserLike {
                IdUser = 1000.ToString()
            };

            // Act
            var result = await _controller.ListLikeSanPham(userlike);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = Assert.IsType<List<SanPhamLike>>(jsonResult.Value);
            Assert.Empty(data);
        }

        // Sp04: Có người dùng like
        [Fact]
        public async Task ListLikeSanPham_ReturnsLikedSanPhams_WhenUserHasLikes() {
            // Arrange
            var userId = 2000;

            var sanpham = new SanPham {
                Ten = "Blouser",
                GiaBan = 2500000
            };
            _context.SanPhams.Add(sanpham);
            await _context.SaveChangesAsync();

            _context.UserLikes.Add(new UserLike {
                IdUser = userId.ToString(),
                IdSanPham = sanpham.Id
            });
            await _context.SaveChangesAsync();

            var userlike = new UserLike {
                IdUser = userId.ToString()
            };

            // Act
            var result = await _controller.ListLikeSanPham(userlike);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = Assert.IsType<List<SanPhamLike>>(jsonResult.Value);
            Assert.Single(data);
            Assert.Equal(sanpham.Ten, data[0].ten);
            Assert.Equal(sanpham.GiaBan, data[0].gia);
        }

        // DeleteLike: K test vi trùng với tc sp02
        
        // ---------------------------- REVIEW ---------------------------
        // Sp05: AddComment
        [Fact]
        public async Task Review_AddsCommentAndReturnsReviewList_WhenValid() {
            // Arrange
            var userId = 3000;
            var sanphamId = 5000;

            var user = new AppUser {
                FirstName = "Minh",
                LastName = "Nguyễn"
            };
            _context.AppUsers.Add(user);
            await _context.SaveChangesAsync();

            var comment = new UserComment {
                IdSanPham = sanphamId,
                IdUser = user.Id,
                Content = "Rất đẹp!"
            };

            // Act
            var result = await _controller.Review(comment);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = Assert.IsType<List<Review>>(jsonResult.Value);

            Assert.Single(data);
            Assert.Equal("Rất đẹp!", data[0].Content);
            Assert.Equal("Minh Nguyễn", data[0].tenUser);
            Assert.True(data[0].NgayComment <= DateTime.Now);
        }

        // Sp06: Trả về tất cả review khi đã có sẵn
        [Fact]
        public async Task Review_ReturnsAllComments_WhenMultipleReviewsExist() {
            // Arrange
            var user1 = new AppUser { FirstName = "A", LastName = "B" };
            var user2 = new AppUser { FirstName = "C", LastName = "D" };
            _context.AppUsers.AddRange(user1, user2);
            await _context.SaveChangesAsync();

            var sanphamId = 6000;

            _context.UserComments.AddRange(
                new UserComment { IdSanPham = sanphamId, IdUser = user1.Id, Content = "Tốt lắm", NgayComment = DateTime.Now },
                new UserComment { IdSanPham = sanphamId, IdUser = user2.Id, Content = "Cũng ổn", NgayComment = DateTime.Now }
            );
            await _context.SaveChangesAsync();

            var comment = new UserComment {
                IdSanPham = sanphamId,
                IdUser = user1.Id,
                Content = "Mới thêm nữa"
            };

            // Act
            var result = await _controller.Review(comment);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = Assert.IsType<List<Review>>(jsonResult.Value);

            Assert.Contains(data, r => r.Content == "Mới thêm nữa");
        }

        // List review k test do trùng tc Sp06

        // -------------------------- CHECK LIKE -------------------------
        // Sp07: Ng dùng chưa like -> 1
        [Fact]
        public async Task CheckLikeSanPham_Returns1_WhenUserHasNotLiked() {
            // Arrange
            var userId = 100;
            var sanphamId = 200;

            var input = new UserLike {
                IdUser = userId.ToString(),
                IdSanPham = sanphamId
            };

            // Act
            var result = await _controller.checkLikeSanPham(input);

            // Assert
            var json = Assert.IsType<JsonResult>(result);
            Assert.Equal(1, json.Value);
        }

        // Sp08: Ng dùng đã like -> 2
        [Fact]
        public async Task CheckLikeSanPham_Returns2_WhenUserHasLiked() {
            // Arrange
            var userId = 101;
            var sanphamId = 201;

            _context.UserLikes.Add(new UserLike {
                IdUser = userId.ToString(),
                IdSanPham = sanphamId
            });
            await _context.SaveChangesAsync();

            var input = new UserLike {
                IdUser = userId.ToString(),
                IdSanPham = sanphamId
            };

            // Act
            var result = await _controller.checkLikeSanPham(input);

            // Assert
            var json = Assert.IsType<JsonResult>(result);
            Assert.Equal(2, json.Value);
        }

        // --------------------- GET ALL -------------------------
        // Sp09: Get all sphams
        [Fact]
        public async Task GetSanPhams_ReturnsListSanpham() {
            // Arrange


            // Act
            var result = await _controller.GetSanPhams();
            var list = result.Value;

            // Assert (chỉ đảm bảo là có dữ liệu)
            Assert.NotNull(list);
            Assert.True(list.Count() >= 0);
        }

        // --------------------- GET BY ID -----------------------
        // Sp10: Id hợp lệ
        [Fact]
        public async Task GetSanPham_ReturnsSanPham_WhenExists() {
            // Arrange
            var sanPham = new SanPham { Ten = "TestSP", GiaBan = 120000 };
            _context.SanPhams.Add(sanPham);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetSanPham(sanPham.Id);

            // Assert
            var okResult = Assert.IsType<ActionResult<SanPham>>(result);
            var value = Assert.IsType<SanPham>(okResult.Value);
            Assert.Equal("TestSP", value.Ten);
        }

        // Sp11: Id k hop le
        [Fact]
        public async Task GetSanPham_ReturnsNotFound_WhenNotExists() {
            // Act
            var result = await _controller.GetSanPham(999); // Không tồn tại

            // Assert
            var actionResult = Assert.IsType<ActionResult<SanPham>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        // --------------------- CAP NHAT TRANG THAI HOAT DONG --------------------
        // Sp12: Cập nhật trạng thái khi dữ liệu hợp lệ
        [Fact]
        public async Task PutSanPhamTrangThaiHoatDong_ShouldReturnOk_WhenSanPhamExists() {
            // Arrange
            var sanPham = new SanPham { Ten = "Áo Thun", GiaBan = 120000, TrangThaiHoatDong = true };
            _context.SanPhams.Add(sanPham);
            await _context.SaveChangesAsync();

            var spUpdate = new SanPham { TrangThaiHoatDong = true };

            // Act
            var result = await _controller.PutSanPhamTrangThaiHoatDong(sanPham.Id, spUpdate);

            // Assert
            Assert.IsType<OkResult>(result);
            var updated = await _context.SanPhams.FindAsync(sanPham.Id);
            Assert.False(updated.TrangThaiHoatDong); // Đã bị đảo từ true -> false
        }

        // -------------------- PUT SAN PHAM --------------------------
        // Sp13: Id k hợp lệ
        [Fact]
        public async Task PutSanPham_ReturnsNotFound_WhenSanPhamDoesNotExist() {
            // Arrange
            var upload = new UploadSanpham { Ten = "Không có", files = null };

            // Act
            var result = await _controller.PutSanPham(888888, upload); // ID không tồn tại

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // Sp14: File is null
        [Fact]
        public async Task PutSanPham_Success_WhenFilesIsNull() {
            // Arrange
            var sanpham = new SanPham { Ten = "Cũ", Id_Loai = 1, Id_NhanHieu = 1, Id_NhaCungCap = 1 };
            _context.SanPhams.Add(sanpham);
            await _context.SaveChangesAsync();

            var upload = new UploadSanpham {
                Ten = "Mới",
                Id_Loai = 2,
                Id_NhanHieu = 2,
                Id_NhaCungCap = 1,
                files = null
            };

            // Act
            var result = await _controller.PutSanPham(sanpham.Id, upload);

            // Assert
            Assert.IsType<OkResult>(result);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == upload.Ten && n.TranType == "Edit");
            Assert.NotNull(notification);
        }

        // Sp15: Valid file
        [Fact]
        public async Task PutSanPham_Success_WithValidFile() {
            // Arrange
            var sanpham = new SanPham { Ten = "SP cũ", Id_Loai = 1, Id_NhanHieu = 1, Id_NhaCungCap = 1 };
            _context.SanPhams.Add(sanpham);
            await _context.SaveChangesAsync();

            var file = new FormFile(new MemoryStream(new byte[4096]), 0, 4096, "Data", "test.jpg") {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var upload = new UploadSanpham {
                Ten = "Quần Short",
                Id_Loai = 2,
                Id_NhanHieu = 2,
                Id_NhaCungCap = 1,
                files = new List<IFormFile> { file }
            };

            // Act
            var result = await _controller.PutSanPham(sanpham.Id, upload);

            // Assert
            Assert.IsType<OkResult>(result);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == upload.Ten && n.TranType == "Edit");
            Assert.NotNull(notification);
        }

        [Theory]
        [InlineData(null, null, null, false)] // Sp16: id nhan hieu - id loai - id ncc : null null null
        [InlineData(1, 2, null, false)]       // Sp17: id nhan hieu - id loai - id ncc : valid valid null
        [InlineData(1, 2, 3, false)]          // Sp18: id nhan hieu - id loai - id ncc : valid valid valid
        public async Task PutSanPham_Should_Update_With_Conditions(
            int? idNhanHieu, int? idLoai, int? idNCC, bool includeFiles) {
            // Arrange
            var id = 123; 
            var upload = new UploadSanpham {
                Ten = "Test SP",
                Id_NhanHieu = idNhanHieu,
                Id_Loai = idLoai,
                Id_NhaCungCap = idNCC,
                files = null
            };

            // Act
            var result = await _controller.PutSanPham(id, upload);

            // Assert
            Assert.IsType<OkResult>(result);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == upload.Ten && n.TranType == "Edit");
            Assert.NotNull(notification);
        }

        // ------------------------------ POST SAN PHAM -----------------------
        // Sp19: Without file
        [Fact]
        public async Task PostSanPham_ReturnsOk_WhenValidDataWithoutFile() {
            var upload = new UploadSanpham {
                Ten = "Test SP",
                HuongDan = "Test HD",
                MoTa = "Test Mô tả",
                ThanhPhan = "Thành phần",
                TrangThaiHoatDong = true,
                TrangThaiSanPham = "",
                GiaBan = 100000,
                GiaNhap = 50000,
                GioiTinh = 1,
                Tag = "TagTest",
                KhuyenMai = 0,
                Id_Loai = 1,
                Id_NhanHieu = 1,
                Id_NhaCungCap = 1,
                files = null
            };

            FakeFileHelper.UploadImageAndReturnFileNameAsync = async (upload, a, b, files, i) =>
            {
                return await Task.FromResult("fake.jpg");
            };

            var result = await _controller.PostSanPham(upload);
            var actionResult = result.Result;

            var ok = Assert.IsType<OkResult>(actionResult);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == upload.Ten && n.TranType == "Add");
            Assert.NotNull(notification);
        }

        // Sp20: WithValidFile
        [Fact]
        public async Task PostSanPham_ReturnsOk_WithValidFiles() {
            var projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../../../API"));

            // Dẫn đến wwwroot/Images/list-image-product
            var imageFolderPath = Path.Combine(projectRoot, "wwwroot", "Images", "list-image-product");

            // Tạo thư mục nếu chưa có
            if (!Directory.Exists(imageFolderPath)) {
                Directory.CreateDirectory(imageFolderPath);
            }

            // Tạo mock file (4KB)
            var fileContent = new byte[4000];
            var fileName = "test.jpg";
            var stream = new MemoryStream(fileContent);
            var file = new FormFile(stream, 0, fileContent.Length, "files", fileName) {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var upload = new UploadSanpham {
                Ten = "Test SP with File",
                HuongDan = "HD",
                MoTa = "Mô tả",
                ThanhPhan = "TP",
                TrangThaiHoatDong = true,
                TrangThaiSanPham = "",
                GiaBan = 150000,
                GiaNhap = 80000,
                GioiTinh = 1,
                Tag = "TagA",
                KhuyenMai = 10,
                Id_Loai = 2,
                Id_NhanHieu = 2,
                Id_NhaCungCap = 2,
                files = new FormFileCollection { file }
            };

            // Giả lập helper (có thể bạn dùng IFileHelper hoặc class static riêng)
            FakeFileHelper.UploadImageAndReturnFileNameAsync = async (upload, a, b, files, i) =>
            {
                // Giả lập là ảnh đã lưu thành công vào folder path kia
                var path = Path.Combine(imageFolderPath, "fake.jpg");
                await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3 }); // tạo file mẫu
                return "fake.jpg";
            };

            // Gọi controller
            var result = await _controller.PostSanPham(upload);
            var actionResult = result.Result;

            // Kiểm tra trả về kết quả thành công
            var ok = Assert.IsType<OkResult>(actionResult);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == upload.Ten && n.TranType == "Add");
            Assert.NotNull(notification);
        }

        // Sp21: Post size with large file
        [Fact]
        public async Task PostSanPham_SkipLargeFiles() {
            var upload = new UploadSanpham {
                Ten = "Test SP Large File",
                HuongDan = "HD",
                MoTa = "Mô tả",
                ThanhPhan = "TP",
                TrangThaiHoatDong = true,
                TrangThaiSanPham = "",
                GiaBan = 150000,
                GiaNhap = 80000,
                GioiTinh = 1,
                Tag = "TagA",
                KhuyenMai = 10,
                Id_Loai = 2,
                Id_NhanHieu = 2,
                Id_NhaCungCap = 2,
                files = new FormFileCollection
                {
                CreateMockFile("largefile.jpg", 9999999)
            }
            };

            FakeFileHelper.UploadImageAndReturnFileNameAsync = async (upload, a, b, files, i) =>
            {
                return await Task.FromResult("fake.jpg");
            };

            var result = await _controller.PostSanPham(upload);
            var actionResult = result.Result;

            var ok = Assert.IsType<OkResult>(actionResult);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == upload.Ten && n.TranType == "Add");
            Assert.NotNull(notification);
        }

        // -------------------- DELETE -------------------------
        // Sp22: Id k hop le
        [Fact]
        public async Task DeleteSanPham_ReturnsNotFound_WhenSanPhamDoesNotExist() {
            // Arrange
            var invalidId = 888999;

            // Act
            var result = await _controller.DeleteSanPham(invalidId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // Sp23: Loai = null, Category = null, Brand = null
        [Fact]
        public async Task DeleteSanPham_DeletesOnlyRelatedData_WhenCategoryAndBrandAreNull() {
            // Arrange
            var sanPham = new SanPham { Ten = "TestSP" };
            _context.SanPhams.Add(sanPham);
            _context.SaveChanges();

            // Act
            var result = await _controller.DeleteSanPham(sanPham.Id);

            // Assert
            Assert.IsType<OkResult>(result);
            Assert.Null(_context.SanPhams.Find(sanPham.Id));
        }

        // Sp24: category ton tai
        [Fact]
        public async Task DeleteSanPham_DeletesSanPham_WhenCategoryExists() {
            // Arrange
            var sanPham = new SanPham { Ten = "TestSP2" };
            _context.SanPhams.Add(sanPham);
            _context.Loais.Add(new Loai { Id = sanPham.Id }); // Category same ID
            _context.SaveChanges();

            // Act
            var result = await _controller.DeleteSanPham(sanPham.Id);

            // Assert
            Assert.IsType<OkResult>(result);
            Assert.Null(_context.SanPhams.Find(sanPham.Id));
        }

        // Sp25: brand ton tai
        [Fact]
        public async Task DeleteSanPham_DeletesSanPham_WhenBrandExists() {
            // Arrange
            var sanPham = new SanPham { Ten = "TestSP3" };
            _context.SanPhams.Add(sanPham);
            await _context.SaveChangesAsync();

            _context.NhanHieus.Add(new NhanHieu { Ten="TEST" });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.DeleteSanPham(sanPham.Id);

            // Assert
            Assert.IsType<OkResult>(result);
            Assert.Null(_context.SanPhams.Find(sanPham.Id));
        }

        // Sp26: category va brand ton tai
        [Fact]
        public async Task DeleteSanPham_DeletesSanPham_WhenBothCategoryAndBrandExist() {
            // Arrange
            var sanPham = new SanPham { Ten = "TestSP4" };
            _context.SanPhams.Add(sanPham);
            _context.Loais.Add(new Loai { Id = sanPham.Id });
            _context.NhanHieus.Add(new NhanHieu { Id = sanPham.Id });
            _context.SaveChanges();

            // Act
            var result = await _controller.DeleteSanPham(sanPham.Id);

            // Assert
            Assert.IsType<OkResult>(result);
            Assert.Null(_context.SanPhams.Find(sanPham.Id));
        }

        // ---------------------- GET CATEGORY ------------------
        // Sp27: loai va nhan hieu k co trong csdl 
        [Fact]
        public async Task GetCategory_ReturnsEmptyList_WhenNoSanPhamMatches() {
            // Arrange
            var testId = 1000;

            _context.SanPhams.Add(new SanPham {Ten = "SP1", Id_Loai = 1, Id_NhanHieu = 2 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetCategory(testId);

            // Assert
            Assert.Empty(result.Value);
        }

        // Sp28: loai co trong csdl
        [Fact]
        public async Task GetCategory_ReturnsSanPham_WhenIdLoaiMatches() {
            // Arrange
            var testId = 200;
            _context.SanPhams.Add(new SanPham {Ten = "SP2", Id_Loai = testId, Id_NhanHieu = 1 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetCategory(testId);

            // Assert
            Assert.Single(result.Value);
            Assert.Equal("SP2", result.Value.First().Ten);
        }

        // Sp29: nhan hieu k co trong csdl
        [Fact]
        public async Task GetCategory_ReturnsSanPham_WhenIdNhanHieuMatches() {
            // Arrange
            var testId = 300;
            _context.SanPhams.Add(new SanPham {Ten = "SP3", Id_Loai = 1, Id_NhanHieu = testId });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetCategory(testId);

            // Assert
            Assert.Single(result.Value);
            Assert.Equal("SP3", result.Value.First().Ten);
        }

        // Sp30: Ca 2 deu co
        [Fact]
        public async Task GetCategory_ReturnsSanPham_WhenBothIdLoaiAndIdNhanHieuMatch() {
            // Arrange
            var testId = 400;
            _context.SanPhams.Add(new SanPham {Ten = "SP4", Id_Loai = testId, Id_NhanHieu = testId });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetCategory(testId);

            // Assert
            Assert.Single(result.Value);
            Assert.Equal("SP4", result.Value.First().Ten);
        }

        // -------------------- NHAN HIEU ----------------------
        // Sp31: Có brand
        [Fact]
        public async Task GetBrand_WhenBrandExists_ReturnsMatchingProducts() {
            // Arrange
            _context.SanPhams.AddRange(
                new SanPham { Ten = "SP1", Id_NhanHieu = 10, Id_Loai = 1 },
                new SanPham { Ten = "SP2", Id_NhanHieu = 10, Id_Loai = 2 },
                new SanPham { Ten = "SP3", Id_NhanHieu = 20, Id_Loai = 3 }
            );

            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetBrand(10);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(2, result.Value.Count());
        }

        // Sp32: K có brand
        [Fact]
        public async Task GetBrand_WhenBrandDoesNotExist_ReturnsEmptyList() {
            // Arrange
            _context.SanPhams.Add(new SanPham { Ten = "SP1", Id_NhanHieu = 30, Id_Loai = 1 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetBrand(999); // Không tồn tại brand

            // Assert
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value);
        }

        // ----------------- LOAI NHAN HIEU ---------------------
        // Bỏ vì Câu if (get != null) luôn trả về true, vì Where(...) luôn trả về một IQueryable, không bao giờ là null.
        // Dù không có dữ liệu nào, get vẫn không phải là null.Kết quả: else không bao giờ được thực thi,
        // nghĩa là code này không bao giờ chạy vào nhánh 2 → không thể đạt phủ cấp 2.

        // ------------------ CHI TIET SAN PHAM --------------------
        // Sp33: Sản phẩm tồn tại
        [Fact]
        public async Task Chitiet_Returns_ProductDetail_When_Exists() {
            // Arrange: Tạo dữ liệu đầy đủ các bảng liên quan
            var loai = new Loai { Ten = "Áo" };
            var nhanHieu = new NhanHieu { Ten = "Nike" };
            var ncc = new NhaCungCap { Ten = "Cty ABC" };
            var size = new Size { TenSize = "M" };
            var mau = new MauSac { MaMau = "Đỏ" };

            _context.Loais.Add(loai);
            _context.NhanHieus.Add(nhanHieu);
            _context.NhaCungCaps.Add(ncc);
            _context.Sizes.Add(size);
            _context.MauSacs.Add(mau);
            await _context.SaveChangesAsync();

            var sp = new SanPham {
                Ten = "Áo Test",
                GiaBan = 100000,
                Id_Loai = loai.Id,
                Id_NhanHieu = nhanHieu.Id,
                Id_NhaCungCap = ncc.Id,
                TrangThaiSanPham = "",
                TrangThaiHoatDong = true
            };
            _context.SanPhams.Add(sp);
            await _context.SaveChangesAsync();

            var image = new ImageSanPham { IdSanPham = sp.Id, ImageName = "img.jpg" };
            var spbt = new SanPhamBienThe {
                Id_SanPham = sp.Id,
                SizeId = size.Id,
                Id_Mau = mau.Id,
                SoLuongTon = 10
            };

            _context.ImageSanPhams.Add(image);
            _context.SanPhamBienThes.Add(spbt);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Chitiet(sp.Id);

            // Assert
            var okResult = Assert.IsType<ActionResult<ProductDetail>>(result);
            var detail = Assert.IsType<ProductDetail>(okResult.Value);
            Assert.Equal("Áo Test", detail.Ten);
            Assert.Single(detail.ImageSanPhams);
            Assert.Single(detail.SanPhamBienThes);
        }

        // Sp34: Sản phẩm k tồn tại
        [Fact]
        public async Task Chitiet_Returns_Null_When_SanPham_NotFound() {
            // Act
            var result = await _controller.Chitiet(98981);

            // Assert
            Assert.Null(result.Value);
        }

        // ------------------ TOP SAN PHAM MOI --------------------
        // Sp35:
        [Fact]
        public async Task DanhSachHangMoi_Returns_Top20_NewActiveProducts() {
            // Arrange
            var loai = new Loai { Ten = "Áo" };
            var nhanHieu = new NhanHieu { Ten = "Adidas" };
            _context.Loais.Add(loai);
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            for (int i = 1; i <= 25; i++) {
                var sp = new SanPham {
                    Ten = $"SP{i}",
                    GiaBan = 100000 + i,
                    Tag = "new arrival",
                    KhuyenMai = 0,
                    MoTa = "Mô tả sản phẩm",
                    HuongDan = "HDSD",
                    GioiTinh = 1,
                    ThanhPhan = "Vải cotton",
                    TrangThaiSanPham = "new",
                    TrangThaiHoatDong = true,
                    Id_Loai = loai.Id,
                    Id_NhanHieu = nhanHieu.Id
                };
                _context.SanPhams.Add(sp);
                await _context.SaveChangesAsync();

                var img = new ImageSanPham {
                    IdSanPham = sp.Id,
                    ImageName = $"img{i}.jpg"
                };
                _context.ImageSanPhams.Add(img);
            }

            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.DanhSachHangMoi();

            // Assert
            var okResult = Assert.IsType<ActionResult<IEnumerable<SanPhamLoaiThuongHieu>>>(result);
            var products = Assert.IsAssignableFrom<IEnumerable<SanPhamLoaiThuongHieu>>(okResult.Value);

            foreach (var p in products) {
                Assert.Equal("new", p.TrangThaiSanPham);
                Assert.True(p.TrangThaiHoatDong);
                Assert.NotNull(p.TenLoai);
                Assert.NotNull(p.TenNhanHieu);
            }
        }

        // -------------------- SAP XEP SAN PHAM -------------------
        // Sp36
        [Fact]
        public async Task SapXepSP_Returns_Products_InPriceRange() {
            // Arrange
            var loai = new Loai { Ten = "Quần" };
            var nhanHieu = new NhanHieu { Ten = "Nike" };
            _context.Loais.Add(loai);
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            for (int i = 1; i <= 10; i++) {
                var sp = new SanPham {
                    Ten = $"SP{i}",
                    GiaBan = i * 100000,
                    Tag = "giảm giá",
                    KhuyenMai = 10,
                    MoTa = "Chi tiết",
                    HuongDan = "Giặt tay",
                    GioiTinh = 1,
                    ThanhPhan = "Vải tổng hợp",
                    TrangThaiSanPham = "available",
                    TrangThaiHoatDong = true,
                    Id_Loai = loai.Id,
                    Id_NhanHieu = nhanHieu.Id
                };
                _context.SanPhams.Add(sp);
                await _context.SaveChangesAsync();

                _context.ImageSanPhams.Add(new ImageSanPham {
                    IdSanPham = sp.Id,
                    ImageName = $"img{i}.jpg"
                });
            }

            await _context.SaveChangesAsync();

            var sapXep = new SapXep { Thap = 200000, Cao = 800000 };

            // Act
            var result = await _controller.SapXepSP(sapXep);

            // Assert
            var okResult = Assert.IsType<JsonResult>(result);
            var data = Assert.IsAssignableFrom<IEnumerable<SanPhamLoaiThuongHieu>>(okResult.Value);
            Assert.All(data, p => Assert.NotNull(p.TenLoai));
            Assert.All(data, p => Assert.NotNull(p.TenNhanHieu));
        }

        [Fact]
        public async Task SearchTheoMau_ReturnsMatchingSanPhams() {
            // Arrange: Setup dữ liệu mẫu
            var mau = new MauSac { MaMau = "Do"};
            _context.MauSacs.Add(mau);
            await _context.SaveChangesAsync();

            var loai = new Loai { Ten = "Áo" };
            var nhanHieu = new NhanHieu { Ten = "Adidas" };
            _context.Loais.Add(loai);
            _context.NhanHieus.Add(nhanHieu);
            await _context.SaveChangesAsync();

            var sp = new SanPham {
                Ten = "Áo Đỏ",
                GiaBan = 500000,
                Tag = "hot",
                KhuyenMai = 15,
                MoTa = "Sản phẩm chất lượng",
                HuongDan = "Giặt tay",
                GioiTinh = 1,
                ThanhPhan = "Cotton",
                TrangThaiSanPham = "available",
                TrangThaiHoatDong = true,
                Id_Loai = loai.Id,
                Id_NhanHieu = nhanHieu.Id
            };
            _context.SanPhams.Add(sp);
            await _context.SaveChangesAsync();

            _context.SanPhamBienThes.Add(new SanPhamBienThe {
                Id_SanPham = sp.Id,
                Id_Mau = mau.Id
            });

            _context.ImageSanPhams.Add(new ImageSanPham {
                IdSanPham = sp.Id,
                ImageName = "do_ao.jpg"
            });

            await _context.SaveChangesAsync();

            var json = JObject.FromObject(new { mausac = "Do" });

            // Act
            var result = await _controller.getListTaskCalendar(json);

            // Assert
            var okResult = Assert.IsType<JsonResult>(result);
            var products = Assert.IsAssignableFrom<IEnumerable<SanPhamLoaiThuongHieu>>(okResult.Value);
            Assert.Single(products);
            Assert.Equal("Áo Đỏ", products.First().Ten);
            Assert.Equal("Adidas", products.First().TenNhanHieu);
            Assert.Equal("Áo", products.First().TenLoai);
            Assert.Equal("do_ao.jpg", products.First().Image);
        }

    }
}
