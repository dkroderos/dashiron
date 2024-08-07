using System.Linq.Expressions;
using MediatR;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Features.Users;

public static class GetUsers
{
    public record Query(string? SearchTerm, string? SortBy, string? SortOrder, int? Page, int? PageSize)
        : IRequest<Result<PagedList<UserResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<UserResponse>>>
    {
        public async Task<Result<PagedList<UserResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            IQueryable<User> usersQuery = context.Users;

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                usersQuery = usersQuery.Where(u => u.Email != default && u.Email.Contains(request.SearchTerm));

            Expression<Func<User, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "email" => user => user.Email!,
                _ => user => user.CreatedAt
            };

            usersQuery = request.SortOrder?.ToLower() == "desc"
                ? usersQuery.OrderByDescending(keySelector)
                : usersQuery.OrderBy(keySelector);

            var userResponsesQuery = usersQuery.Select(u => new UserResponse(u.Id, u.Email!));

            var users = await PagedList<UserResponse>.CreateAsync(userResponsesQuery, request.Page ?? 1,
                request.PageSize ?? 10);

            return users;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users",
                    async (string? searchTerm, string? sortBy, string? sortOrder, int? page, int? pageSize,
                        ISender sender) =>
                    {
                        var query = new Query(searchTerm, sortBy, sortOrder, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                    })
                .RequireAuthorization()
                .WithTags(nameof(User));
        }
    }
}