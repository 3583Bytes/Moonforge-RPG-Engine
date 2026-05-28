namespace Moonforge.Core.Runtime.Queries
{

    public interface IQueryHandler<in TQuery, out TResult> where TQuery : IQuery<TResult>
    {
        TResult Query(GameState gameState, TQuery query);
    }
}
