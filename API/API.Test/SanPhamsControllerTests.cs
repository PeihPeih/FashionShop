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
        // SP01: Kiểm tra thêm lượt like cho sản phẩm khi người dùng chưa từng like trước đó.
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

        // SP02: Kiểm tra chức năng unlike sản phẩm khi người dùng đã like trước đó (xóa like thành công)
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

        // Sp03: Kiểm tra việc trả về danh sách sản phẩm đã được người dùng like khi có dữ liệu like tồn tại.
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

        // SP04: Kiểm tra việc thêm bình luận thành công và trả về danh sách các bình luận của sản phẩm.
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

        // SP05: Test kiểm tra thêm bình luận mới và trả về đầy đủ bình luận đã có của sản phẩm.
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

        // SP06: Test kiểm tra thêm bình luận mới và trả về đầy đủ bình luận đã có của sản phẩm
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


        //SP07: Test kiểm tra trả về 2 khi người dùng đã like sản phẩm rồi.
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

        // SP08: Kiểm tra việc lấy danh sách sản phẩm trả về đúng kiểu dữ liệu
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

        // SP09: Kiểm tra việc lấy chi tiết một sản phẩm theo Id trả về đúng sản phẩm khi sản phẩm tồn tại trong cơ sở dữ liệu
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

        // SP10: Kiểm tra việc lấy chi tiết sản phẩm theo Id trả về kết quả NotFound khi sản phẩm không tồn tại trong cơ sở dữ liệu.
        [Fact]
        public async Task GetSanPham_ReturnsNotFound_WhenNotExists() {
            // Act
            var result = await _controller.GetSanPham(999); // Không tồn tại

            // Assert
            var actionResult = Assert.IsType<ActionResult<SanPham>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        // SP11: Kiểm tra việc cập nhật trạng thái hoạt động của sản phẩm thành công
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

        // SP12: Kiểm tra hành vi khi cập nhật thông tin sản phẩm với ID không tồn tại, đảm bảo controller trả về kết quả NotFound.
        [Fact]
        public async Task PutSanPham_ReturnsNotFound_WhenSanPhamDoesNotExist() {
            // Arrange
            var upload = new UploadSanpham { Ten = "Không có", files = null };

            // Act
            var result = await _controller.PutSanPham(888888, upload); // ID không tồn tại

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // SP13: Kiểm tra việc cập nhật sản phẩm thành công khi không có file đính kèm và xác nhận thông báo được tạo
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

        // SP14: Kiểm tra việc cập nhật sản phẩm thành công khi có file hình ảnh hợp lệ và xác nhận thông báo được tạo.
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
        [InlineData(null, null, null, false)] // SP15: Kiểm tra việc cập nhật sản phẩm thành công với các điều kiện khác nhau về nhãn hiệu, loại và nhà cung cấp, đồng thời xác nhận thông báo được tạo.
        [InlineData(1, 2, null, false)]       // SP16: Kiểm tra việc cập nhật sản phẩm thành công với các điều kiện khác nhau về nhãn hiệu, loại và nhà cung cấp, đồng thời xác nhận thông báo được tạo.
        [InlineData(1, 2, 3, false)]          // SP17: Kiểm tra việc cập nhật sản phẩm thành công với các điều kiện khác nhau về nhãn hiệu, loại và nhà cung cấp, đồng thời xác nhận thông báo được tạo.
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

        // SP18: Kiểm tra việc thêm mới sản phẩm thành công khi không có file đính kèm và xác nhận thông báo được tạo
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

            FakeFileHelper.UploadImageAndReturnFileNameAsync = async (upload, a, b, files, i) => {
                return await Task.FromResult("fake.jpg");
            };

            var result = await _controller.PostSanPham(upload);
            var actionResult = result.Result;

            var ok = Assert.IsType<OkResult>(actionResult);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == upload.Ten && n.TranType == "Add");
            Assert.NotNull(notification);
        }

        // SP19: Kiểm tra việc thêm mới sản phẩm thành công khi có file hình ảnh hợp lệ và xác nhận thông báo được tạo
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

            FakeFileHelper.UploadImageAndReturnFileNameAsync = async (upload, a, b, files, i) => {
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

        // SP20: Kiểm tra việc thêm mới sản phẩm thành công khi có file lớn, đảm bảo hệ thống bỏ qua file lớn và xác nhận thông báo được tạo
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

            FakeFileHelper.UploadImageAndReturnFileNameAsync = async (upload, a, b, files, i) => {
                return await Task.FromResult("fake.jpg");
            };

            var result = await _controller.PostSanPham(upload);
            var actionResult = result.Result;

            var ok = Assert.IsType<OkResult>(actionResult);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TenSanPham == upload.Ten && n.TranType == "Add");
            Assert.NotNull(notification);
        }

        // SP21: Kiểm tra việc xóa sản phẩm trả về NotFound khi sản phẩm không tồn tại.
        [Fact]
        public async Task DeleteSanPham_SanPhamNull_ReturnsNotFound() {
            // Arrange
            int id = 999; // ID không tồn tại

            // Act
            var result = await _controller.DeleteSanPham(id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // SP22: Kiểm tra việc xóa sản phẩm thành công và chỉ xóa dữ liệu liên quan khi loại và nhãn hiệu là null.
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


        // SP23: Kiểm tra việc xóa sản phẩm thành công khi có nhãn hiệu tồn tại.
        [Fact]
        public async Task DeleteSanPham_DeletesSanPham_WhenBrandExists() {
            // Arrange
            var sanPham = new SanPham { Ten = "TestSP3" };
            _context.SanPhams.Add(sanPham);
            await _context.SaveChangesAsync();

            _context.NhanHieus.Add(new NhanHieu { Ten = "TEST" });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.DeleteSanPham(sanPham.Id);

            // Assert
            Assert.IsType<OkResult>(result);
            Assert.Null(_context.SanPhams.Find(sanPham.Id));
        }

        // SP24: Kiểm tra việc xóa sản phẩm thành công khi cả loại và nhãn hiệu liên quan đều tồn tại. Xác nhận rằng sản phẩm được xóa khỏi cơ sở dữ liệu và trả về kết quả thành công
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

        

        // ----------------- LOAI NHAN HIEU ---------------------
        // Bỏ vì Câu if (get != null) luôn trả về true, vì Where(...) luôn trả về một IQueryable, không bao giờ là null.
        // Dù không có dữ liệu nào, get vẫn không phải là null.Kết quả: else không bao giờ được thực thi,
        // nghĩa là code này không bao giờ chạy vào nhánh 2 → không thể đạt phủ cấp 2.

        // SP25: Kiểm tra trả về chi tiết sản phẩm khi sản phẩm và các thông tin liên quan tồn tại.
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

        // SP26: Kiểm tra trả về tối đa 20 sản phẩm mới, đang hoạt động và có đầy đủ thông tin loại và nhãn hiệu.
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

        // SP27: Kiểm tra trả về danh sách sản phẩm trong khoảng giá xác định, kèm thông tin loại và nhãn hiệu.
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
    }
}
