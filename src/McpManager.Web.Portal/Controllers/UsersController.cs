using System.Security.Claims;
using McpManager.Core.Data.Models.Identity;
using McpManager.Core.Repositories;
using McpManager.Core.Repositories.ApiKeys;
using McpManager.Core.Repositories.Identity;
using McpManager.Core.Repositories.Mcp;
using McpManager.Core.Repositories.Notifications;
using McpManager.Web.Portal.Authorization;
using McpManager.Web.Portal.Controllers.Abstract;
using McpManager.Web.Portal.Dtos.Users;
using McpManager.Web.Portal.Services.FlashMessage.Contracts;
using McpManager.Web.Portal.TagHelpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace McpManager.Web.Portal.Controllers;

public class UsersController : BaseController
{
    private readonly UserManager<User> _userManager;
    private readonly UserRepository _userRepository;
    private readonly IFlashMessage _flashMessage;

    public UsersController(
        UserManager<User> userManager,
        UserRepository userRepository,
        IFlashMessage flashMessage
    )
    {
        _userManager = userManager;
        _userRepository = userRepository;
        _flashMessage = flashMessage;
    }

    public IActionResult Index(TextSearchDto filters)
    {
        filters ??= new TextSearchDto();
        ViewData["Title"] = "Users";
        ViewData["Menu"] = "Users";
        ViewData["Icon"] = HeroIcons.Render("users", size: 5);
        ViewData["Filters"] = filters;

        var query = _userRepository.GetAll();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.ToLower();
            query = query.Where(u =>
                u.GivenName.ToLower().Contains(search)
                || u.Surname.ToLower().Contains(search)
                || u.Email.ToLower().Contains(search)
            );
        }

        query = query.OrderBy(u => u.GivenName).ThenBy(u => u.Surname);

        return View(query);
    }

    public async Task<IActionResult> Show(Guid id)
    {
        ViewData["Menu"] = "Users";
        ViewData["Icon"] = HeroIcons.Render("user", size: 5);

        var user = await _userRepository.Get(id);
        if (user == null)
        {
            _flashMessage.Error("User not found.");
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = user.FullName;
        ViewData["ClaimGroups"] = ClaimStore.ClaimGroups();

        return View(user);
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Create User";
        ViewData["Menu"] = "Users";
        ViewData["Icon"] = HeroIcons.Render("user-plus", size: 5);

        return View(
            new UserForCreateDto { Claims = BuildClaimCheckboxItems(new HashSet<string>()) }
        );
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserForCreateDto dto)
    {
        ViewData["Title"] = "Create User";
        ViewData["Menu"] = "Users";
        ViewData["Icon"] = HeroIcons.Render("user-plus", size: 5);

        if (!ModelState.IsValid)
        {
            return View(dto);
        }

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(nameof(dto.Email), "A user with this email already exists.");
            return View(dto);
        }

        var user = new User
        {
            GivenName = dto.GivenName,
            Surname = dto.Surname,
            Email = dto.Email,
            UserName = dto.Email,
            IsActive = dto.IsActive,
            EmailConfirmed = dto.EmailConfirmed,
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(dto);
        }

        var enabledClaims = dto.Claims.Where(c => c.IsSelected).Select(c => c.Type).ToList();
        await UpdateUserClaims(user, enabledClaims);

        _flashMessage.Success("User created successfully.");
        return RedirectToAction(nameof(Show), new { id = user.Id });
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        ViewData["Title"] = "Edit User";
        ViewData["Menu"] = "Users";
        ViewData["Icon"] = HeroIcons.Render("pencil-square", size: 5);

        var user = await _userRepository.Get(id);
        if (user == null)
        {
            _flashMessage.Error("User not found.");
            return RedirectToAction(nameof(Index));
        }

        var enabledClaims = user.Claims.Select(c => c.ClaimType).ToHashSet();
        var dto = new UserForEditDto
        {
            GivenName = user.GivenName,
            Surname = user.Surname,
            Email = user.Email,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            Claims = BuildClaimCheckboxItems(enabledClaims),
        };

        ViewData["EditUser"] = user;

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, UserForEditDto dto)
    {
        ViewData["Title"] = "Edit User";
        ViewData["Menu"] = "Users";
        ViewData["Icon"] = HeroIcons.Render("pencil-square", size: 5);

        var user = await _userRepository.Get(id);
        if (user == null)
        {
            _flashMessage.Error("User not found.");
            return RedirectToAction(nameof(Index));
        }

        ViewData["EditUser"] = user;

        if (!ModelState.IsValid)
        {
            return View(dto);
        }

        // Check if email is changing and if it conflicts with another user
        if (!user.Email.Equals(dto.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                ModelState.AddModelError(
                    nameof(dto.Email),
                    "A user with this email already exists."
                );
                return View(dto);
            }
            user.UserName = dto.Email;
            user.Email = dto.Email;
        }

        user.GivenName = dto.GivenName;
        user.Surname = dto.Surname;
        user.IsActive = dto.IsActive;
        user.EmailConfirmed = dto.EmailConfirmed;

        // Update password if provided
        if (!string.IsNullOrEmpty(dto.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, dto.Password);
            if (!passwordResult.Succeeded)
            {
                foreach (var error in passwordResult.Errors)
                {
                    ModelState.AddModelError(nameof(dto.Password), error.Description);
                }
                return View(dto);
            }
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(dto);
        }

        // Update claims
        var enabledClaims = dto.Claims.Where(c => c.IsSelected).Select(c => c.Type).ToList();
        await UpdateUserClaims(user, enabledClaims);

        _flashMessage.Success("User updated successfully.");
        return RedirectToAction(nameof(Show), new { id });
    }

    private static List<ClaimCheckboxItem> BuildClaimCheckboxItems(HashSet<string> enabledClaims)
    {
        return ClaimStore
            .ClaimGroups()
            .SelectMany(g =>
                g.Claims.Select(c => new ClaimCheckboxItem
                {
                    Type = c.Type,
                    Name = c.Name,
                    Description = c.Description,
                    Group = g.Name,
                    IsSelected = enabledClaims.Contains(c.Type),
                })
            )
            .ToList();
    }

    private async Task UpdateUserClaims(User user, List<string> enabledClaims)
    {
        var currentClaims = user.Claims.Select(c => c.ClaimType).ToList();
        var allClaimTypes = ClaimStore.ClaimList().Select(c => c.Type).ToList();

        // Remove claims that are no longer enabled
        var claimsToRemove = currentClaims
            .Where(c => !enabledClaims.Contains(c) && allClaimTypes.Contains(c))
            .ToList();
        foreach (var claimType in claimsToRemove)
        {
            await _userManager.RemoveClaimAsync(user, new Claim(claimType, claimType));
        }

        // Add claims that are newly enabled
        var claimsToAdd = enabledClaims
            .Where(c => !currentClaims.Contains(c) && allClaimTypes.Contains(c))
            .ToList();
        foreach (var claimType in claimsToAdd)
        {
            await _userManager.AddClaimAsync(user, new Claim(claimType, claimType));
        }
    }
}
