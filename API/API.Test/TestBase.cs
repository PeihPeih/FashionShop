using API.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace API.Test {
    public class TestBase : IDisposable {
        protected readonly DbContextOptions<DPContext> _options;
        protected readonly TransactionScope _scope;

        public TestBase() {
            _options = new DbContextOptionsBuilder<DPContext>()
                .UseSqlServer("Server=DESKTOP-P4V9CNG\\CSSLPTSUB1;Database=EFashionShop;Integrated Security=True")
                .Options;

            // Bắt đầu một TransactionScope mới, cho phép rollback
            _scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        }

        public void Dispose() {
            // Rollback tất cả thay đổi trong scope
            _scope.Dispose();
        }
    }

}
