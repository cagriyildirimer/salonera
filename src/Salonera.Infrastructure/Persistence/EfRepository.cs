using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Salonera.Application.Common.Persistence;
using Salonera.Domain.Common;

namespace Salonera.Infrastructure.Persistence;

public sealed class EfRepository<TEntity>(SaloneraDbContext context) : IRepository<TEntity>
    where TEntity : AuditableEntity
{
    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<TEntity>().SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> query = context.Set<TEntity>().AsNoTracking();
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return context.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();
    }

    public void Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        context.Set<TEntity>().Update(entity);
    }

    public void SoftDelete(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.IsDeleted = true;
        context.Set<TEntity>().Update(entity);
    }
}
