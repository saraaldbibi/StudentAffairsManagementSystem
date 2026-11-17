using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StudentAffairsManagementSystem.Controllers
{
    [Route("auth-test")]
    public class AuthTestController : Controller
    {
        // متاح للجميع دون تسجيل دخول
        [HttpGet("public")]
        [AllowAnonymous]
        public IActionResult Public()
        {
            return Ok("Public endpoint works");
        }

        // يتطلب تسجيل الدخول
        [HttpGet("private")]
        [Authorize]
        public IActionResult Private()
        {
            return Ok("Authenticated user");
        }

        // يتطلب صلاحية المدير
        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        public IActionResult Admin()
        {
            return Ok("Admin access granted");
        }

        // يتطلب صلاحية الموظف
        [HttpGet("employee")]
        [Authorize(Roles = "Employee")]
        public IActionResult Employee()
        {
            return Ok("Employee access granted");
        }

        // يتطلب صلاحية الطالب
        [HttpGet("student")]
        [Authorize(Roles = "Student")]
        public IActionResult Student()
        {
            return Ok("Student access granted");
        }
    }
}