using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.EntityFrameworkCore.Query.Internal;
using DelegateDecompiler.JIT;

namespace DelegateDecompiler.EntityFrameworkCore;

[SuppressMessage("Usage", "EF1001:Internal EF Core API usage.")]
class DelegateDecompileEntityQueryProvider(IQueryCompiler queryCompiler) : EntityQueryProvider(queryCompiler)
{
    public override TResult Execute<TResult>(Expression expression) =>
        base.Execute<TResult>(ExpressionFactoryVisitor.Build(expression));

    public override object Execute(Expression expression) =>
        base.Execute(ExpressionFactoryVisitor.Build(expression));

    public override TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default) =>
        base.ExecuteAsync<TResult>(ExpressionFactoryVisitor.Build(expression), cancellationToken);
}