using Atlas.Features.Image.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Features.Image;

public static class ImageEndpoints
{
    public static IEndpointRouteBuilder MapImageEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/images").WithTags("Images")
            .RequireAuthorization();

        group.MapGet("/", GetAllImages);
        // 绑定 IFormFile 的端点会自动带上防伪校验元数据，而本项目没有启用 UseAntiforgery，
        // 不关闭会在请求时抛 "a middleware was not found that supports anti-forgery"（500）
        // 认证 Cookie 是 SameSite=Lax，跨站表单请求不会携带，关闭是安全的
        group.MapPost("/", UploadImage).DisableAntiforgery();
        group.MapPost("/favicon", FetchFavicon);
        group.MapDelete("/{imageId}", DeleteImage);

        return endpoints;
    }

    private static async Task<Results<Ok<List<ImageResponse>>, ProblemHttpResult>> GetAllImages(
        [FromQuery] string? kind, ImageService service, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(kind) && !ImageService.IsValidKind(kind))
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest);

        var responses = await service.GetAllImagesAsync(
            string.IsNullOrEmpty(kind) ? null : kind, ct);
        return TypedResults.Ok(responses);
    }

    private static async Task<Results<Created<ImageResponse>, ProblemHttpResult>> UploadImage(
        IFormFile? file, [FromQuery] string? kind, ImageService service, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > ImageService.MaxFileSize)
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest);

        if (!ImageService.AllowedExtensions.Contains(Path.GetExtension(file.FileName)))
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest);

        if (!string.IsNullOrEmpty(kind) && !ImageService.IsValidKind(kind))
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest);

        var image = await service.UploadImageAsync(
            file, string.IsNullOrEmpty(kind) ? ImageService.KindImage : kind, ct);
        return TypedResults.Created($"/api/images/{image.Id}", image);
    }

    private static async Task<Results<Created<ImageResponse>, ProblemHttpResult>> FetchFavicon(
        FaviconRequest request, FaviconService service, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest);

        var image = await service.FetchAndSaveAsync(request.Url, ct);
        return image is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest)
            : TypedResults.Created($"/api/images/{image.Id}", image);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteImage(
        int imageId, ImageService service, CancellationToken ct)
    {
        var ok = await service.DeleteImageAsync(imageId, ct);
        return ok
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
    }
}