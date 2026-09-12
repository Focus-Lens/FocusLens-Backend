using FocusLens.Application.Features.Identity.Commands.ForgotPassword;
using FocusLens.Application.Features.Identity.Commands.GoogleLogin;
using FocusLens.Application.Features.Identity.Commands.Login;
using FocusLens.Application.Features.Identity.Commands.RefreshToken;
using FocusLens.Application.Features.Identity.Commands.RegisterParent;
using FocusLens.Application.Features.Identity.Commands.RegisterStudent;
using FocusLens.Application.Features.Identity.Commands.ResendVerificationCode;
using FocusLens.Application.Features.Identity.Commands.ResetPassword;
using FocusLens.Application.Features.Identity.Commands.RevokeRefreshToken;
using FocusLens.Application.Features.Identity.Commands.VerifyEmail;
using FocusLens.Application.Features.Identity.Dtos;
using FocusLens.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[Route("api/auth")]
public sealed class AuthController : ApiController
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [AllowAnonymous]
    [HttpPost("register/student")]
    public async Task<IActionResult> RegisterStudent(
        RegisterStudentCommand command,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            _ => NoContent(),
            Problem);
    }

    [AllowAnonymous]
    [HttpPost("register/parent")]
    public async Task<IActionResult> RegisterParent(
        RegisterParentCommand command,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            _ => NoContent(),
            Problem);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        Result<AuthResponse> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [AllowAnonymous]
    [HttpPost("continue-with-google")]
    public async Task<IActionResult> ContinueWithGoogle(
        GoogleLoginCommand command,
        CancellationToken cancellationToken)
    {
        Result<AuthResponse> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        Result<TokenResponse> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            Ok,
            Problem);
    }


    [HttpPost("logout")]
    public async Task<IActionResult> RevokeRefreshToken(
        RevokeRefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            _ => NoContent(),
            Problem);
    }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailCommand command,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            _ => NoContent(),
            Problem);
    }

    [AllowAnonymous]
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerificationCode(
        ResendVerificationCodeCommand command,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            _ => NoContent(),
            Problem);
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            _ => NoContent(),
            Problem);
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await _sender.Send(command, cancellationToken);

        return result.Match(
            _ => NoContent(),
            Problem);
    }
}