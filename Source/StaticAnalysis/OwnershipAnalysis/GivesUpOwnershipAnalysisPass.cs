﻿//-----------------------------------------------------------------------
// <copyright file="GivesUpOwnershipAnalysisPass.cs">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//      EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//      MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//      IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//      CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//      TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//      SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.DataFlowAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.PSharp.StaticAnalysis
{
    /// <summary>
    /// This analysis checks if any method in each machine of a P# program
    /// is erroneously giving up ownership of references.
    /// </summary>
    internal sealed class GivesUpOwnershipAnalysisPass : OwnershipAnalysisPass
    {
        #region internal API

        /// <summary>
        /// Creates a new gives-up ownership analysis pass.
        /// </summary>
        /// <param name="context">AnalysisContext</param>
        /// <returns>GivesUpOwnershipAnalysisPass</returns>
        internal static GivesUpOwnershipAnalysisPass Create(PSharpAnalysisContext context)
        {
            return new GivesUpOwnershipAnalysisPass(context);
        }

        #endregion

        #region protected methods

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the control-flow graph.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        protected override void AnalyzeOwnershipInControlFlowGraph(GivenUpOwnershipSymbol givenUpSymbol,
            StateMachine originalMachine, SemanticModel model, TraceInfo trace)
        {
            var queue = new Queue<ControlFlowGraphNode>();
            queue.Enqueue(givenUpSymbol.Statement.ControlFlowGraphNode);

            var visitedNodes = new HashSet<ControlFlowGraphNode>();
            visitedNodes.Add(givenUpSymbol.Statement.ControlFlowGraphNode);

            bool repeatGivesUpNode = false;
            while (queue.Count > 0)
            {
                ControlFlowGraphNode node = queue.Dequeue();

                var statements = new List<Statement>();
                if (!repeatGivesUpNode &&
                    node.Equals(givenUpSymbol.Statement.ControlFlowGraphNode))
                {
                    statements.AddRange(node.Statements.TakeWhile(val
                        => !val.Equals(givenUpSymbol.Statement)));
                    statements.Add(givenUpSymbol.Statement);
                }
                else
                {
                    statements.AddRange(node.Statements);
                }

                foreach (var statement in statements)
                {
                    base.AnalyzeOwnershipInStatement(givenUpSymbol, statement,
                        originalMachine, model, trace);
                }

                foreach (var predecessor in node.GetImmediatePredecessors())
                {
                    if (!repeatGivesUpNode &&
                        predecessor.Equals(givenUpSymbol.Statement.ControlFlowGraphNode))
                    {
                        repeatGivesUpNode = true;
                        visitedNodes.Remove(givenUpSymbol.Statement.ControlFlowGraphNode);
                    }

                    if (!visitedNodes.Contains(predecessor))
                    {
                        queue.Enqueue(predecessor);
                        visitedNodes.Add(predecessor);
                    }
                }
            }
        }

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the variable declaration.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="varDecl">VariableDeclarationSyntax</param>
        /// <param name="statement">Statement</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        protected override void AnalyzeOwnershipInLocalDeclaration(GivenUpOwnershipSymbol givenUpSymbol,
            VariableDeclarationSyntax varDecl, Statement statement, StateMachine originalMachine,
            SemanticModel model, TraceInfo trace)
        {
            foreach (var variable in varDecl.Variables.Where(v => v.Initializer != null))
            {
                ExpressionSyntax expr = variable.Initializer.Value;
                ISymbol leftSymbol = model.GetDeclaredSymbol(variable);

                this.AnalyzeGivingUpFieldOwnership(givenUpSymbol, leftSymbol, statement, trace);
                this.AnalyzeOwnershipInExpression(givenUpSymbol, expr, statement,
                    originalMachine, model, trace);
            }
        }

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the assignment expression.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="assignment">AssignmentExpressionSyntax</param>
        /// <param name="statement">Statement</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        protected override void AnalyzeOwnershipInAssignment(GivenUpOwnershipSymbol givenUpSymbol,
            AssignmentExpressionSyntax assignment, Statement statement, StateMachine originalMachine,
            SemanticModel model, TraceInfo trace)
        {
            IdentifierNameSyntax leftIdentifier = CodeAnalysis.CSharp.DataFlowAnalysis.
                AnalysisContext.GetTopLevelIdentifier(assignment.Left);
            ISymbol leftSymbol = model.GetSymbolInfo(leftIdentifier).Symbol;
            
            this.AnalyzeGivingUpFieldOwnership(givenUpSymbol, leftSymbol, statement, trace);
            this.AnalyzeOwnershipInExpression(givenUpSymbol, assignment.Right,
                statement, originalMachine, model, trace);
        }

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the candidate callee.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="calleeSummary">MethodSummary</param>
        /// <param name="call">ExpressionSyntax</param>
        /// <param name="statement">Statement</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        protected override void AnalyzeOwnershipInCandidateCallee(GivenUpOwnershipSymbol givenUpSymbol,
            MethodSummary calleeSummary, ExpressionSyntax call, Statement statement,
            StateMachine originalMachine, SemanticModel model, TraceInfo trace)
        {
            ArgumentListSyntax argumentList = base.AnalysisContext.GetArgumentList(call);
            if (argumentList == null)
            {
                return;
            }

            for (int idx = 0; idx < argumentList.Arguments.Count; idx++)
            {
                var argIdentifier = CodeAnalysis.CSharp.DataFlowAnalysis.AnalysisContext.
                    GetTopLevelIdentifier(argumentList.Arguments[idx].Expression);
                if (argIdentifier == null)
                {
                    continue;
                }

                ISymbol argSymbol = model.GetSymbolInfo(argIdentifier).Symbol;
                if (DataFlowAnalysisEngine.FlowsIntoSymbol(argSymbol, givenUpSymbol.ContainingSymbol,
                    statement, givenUpSymbol.Statement))
                {
                    if (calleeSummary.SideEffects.Any(v => v.Value.Contains(idx) &&
                        base.IsFieldAccessedBeforeBeingReset(v.Key, statement.GetMethodSummary())))
                    {
                        AnalysisErrorReporter.ReportGivenUpFieldOwnershipError(trace, argSymbol);
                    }
                }
            }
        }

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the gives-up operation.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="call">Gives-up call</param>
        /// <param name="statement">Statement</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        protected override void AnalyzeOwnershipInGivesUpCall(GivenUpOwnershipSymbol givenUpSymbol,
            InvocationExpressionSyntax call, Statement statement, SemanticModel model, TraceInfo trace)
        {
            if (givenUpSymbol.Statement.Equals(statement) &&
                givenUpSymbol.ContainingSymbol.Kind == SymbolKind.Field &&
                //!DataFlowQuerying.DoesResetInSuccessorControlFlowGraphNodes(givenUpSymbol.ContainingSymbol,
                //givenUpSymbol.ContainingSymbol, statement) &&
                base.IsFieldAccessedBeforeBeingReset(givenUpSymbol.ContainingSymbol, statement.GetMethodSummary()))
            {
                AnalysisErrorReporter.ReportGivenUpFieldOwnershipError(trace, givenUpSymbol.ContainingSymbol);
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">AnalysisContext</param>
        private GivesUpOwnershipAnalysisPass(PSharpAnalysisContext context)
            : base(context)
        {

        }

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the expression.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="expr">ExpressionSyntax</param>
        /// <param name="statement">Statement</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        private void AnalyzeOwnershipInExpression(GivenUpOwnershipSymbol givenUpSymbol,
            ExpressionSyntax expr, Statement statement, StateMachine originalMachine,
            SemanticModel model, TraceInfo trace)
        {
            if (expr is IdentifierNameSyntax ||
                expr is MemberAccessExpressionSyntax)
            {
                IdentifierNameSyntax rightIdentifier = CodeAnalysis.CSharp.DataFlowAnalysis.
                    AnalysisContext.GetTopLevelIdentifier(expr);
                if (rightIdentifier != null)
                {
                    var rightSymbol = model.GetSymbolInfo(rightIdentifier).Symbol;
                    this.AnalyzeGivingUpFieldOwnership(givenUpSymbol, rightSymbol, statement, trace);
                }
            }
            else if (expr is InvocationExpressionSyntax ||
                expr is ObjectCreationExpressionSyntax)
            {
                trace.InsertCall(statement.GetMethodSummary().Method, expr);

                HashSet<ISymbol> returnSymbols = base.AnalyzeOwnershipInCall(givenUpSymbol,
                    expr, statement, originalMachine, model, trace);
                foreach (var returnSymbol in returnSymbols)
                {
                    this.AnalyzeGivingUpFieldOwnership(givenUpSymbol, returnSymbol, statement, trace);
                }
            }
        }

        /// <summary>
        /// Analyzes the given-up ownership of fields in the expression.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="symbol">Symbol</param>
        /// <param name="statement">Statement</param>
        /// <param name="trace">TraceInfo</param>
        private void AnalyzeGivingUpFieldOwnership(GivenUpOwnershipSymbol givenUpSymbol,
            ISymbol symbol, Statement statement, TraceInfo trace)
        {
            if (!DataFlowAnalysisEngine.FlowsIntoSymbol(symbol, givenUpSymbol.ContainingSymbol,
                statement, givenUpSymbol.Statement))
            {
                return;
            }
            
            if (symbol.Kind == SymbolKind.Field &&
                //!DataFlowQuerying.DoesResetInSuccessorControlFlowGraphNodes(symbol,
                //givenUpSymbol.ContainingSymbol, syntaxNode, cfgNode) &&
                base.IsFieldAccessedBeforeBeingReset(symbol, statement.GetMethodSummary()))
            {
                TraceInfo newTrace = new TraceInfo();
                newTrace.Merge(trace);
                newTrace.AddErrorTrace(statement.SyntaxNode);

                AnalysisErrorReporter.ReportGivenUpFieldOwnershipError(newTrace, symbol);
            }
        }

        #endregion 
    }
}