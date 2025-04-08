using API.Dtos;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace API.Test {
    public class FakeFileHelper {
        public static Func<UploadSanpham, object, string, IFormFile[], int, Task<string>> UploadImageAndReturnFileNameAsync
        = async (upload, a, b, files, i) => await Task.FromResult("test.jpg");
    }

}
