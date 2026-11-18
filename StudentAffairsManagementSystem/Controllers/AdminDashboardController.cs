
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentAffairsManagementSystem.Data;

namespace StudentAffairsManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminDashboardController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.StudentsCount =
                await _context.Students.CountAsync();

            ViewBag.EmployeesCount =
                await _context.Employees.CountAsync();

            ViewBag.FacultiesCount =
                await _context.Faculties.CountAsync();

            ViewBag.DepartmentsCount =
                await _context.Departments.CountAsync();

            ViewBag.CoursesCount =
                await _context.Courses.CountAsync();

            ViewBag.RegistrationsCount =
                await _context.Registrations.CountAsync();

            return View();
        }
    }
}
