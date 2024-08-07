using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Challenges;

public static class DeleteChallenge
{
    public record Command(Guid Id) : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.Id)
                .Include(c => c.ChallengeFiles)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure(new Error("DeleteChallenge.NotFound",
                    "The challenge with the specified ID was not found"));

            context.ChallengeFiles.RemoveRange(challenge.ChallengeFiles);

            context.Challenges.Remove(challenge);

            await context.SaveChangesAsync(cancellationToken);

            foreach (var file in challenge.ChallengeFiles)
                await cache.RemoveAsync($"{nameof(ChallengeFile)}:{file.Id}", token: cancellationToken);

            await cache.RemoveAsync($"{nameof(Challenge)}:{challenge.Id}", token: cancellationToken);
            await cache.RemoveAsync($"{nameof(ChallengeResponse)}:{challenge.Id}", token: cancellationToken);
            await cache.RemoveAsync($"{nameof(Challenge)}.{nameof(Challenge.Flags)}:{challenge.Id}",
                token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("challenges/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Command(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization()
                .WithTags(nameof(Challenge));
        }
    }
}