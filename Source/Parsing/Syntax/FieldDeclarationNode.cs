﻿//-----------------------------------------------------------------------
// <copyright file="FieldDeclarationNode.cs">
//      Copyright (c) 2015 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
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
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PSharp.Parsing.Syntax
{
    /// <summary>
    /// Field declaration node.
    /// </summary>
    public sealed class FieldDeclarationNode : PSharpSyntaxNode
    {
        #region fields

        /// <summary>
        /// The machine parent node.
        /// </summary>
        private MachineDeclarationNode Machine;

        /// <summary>
        /// The modifier token.
        /// </summary>
        public Token Modifier;

        /// <summary>
        /// The type identifier token
        /// </summary>
        public TypeIdentifierNode TypeIdentifier;

        /// <summary>
        /// The identifier token.
        /// </summary>
        public Token Identifier;

        /// <summary>
        /// The semicolon token.
        /// </summary>
        public Token SemicolonToken;

        #endregion

        #region public API

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="machineNode">MachineDeclarationNode</param>
        public FieldDeclarationNode(MachineDeclarationNode machineNode)
        {
            this.Machine = machineNode;
        }

        /// <summary>
        /// Returns the full text.
        /// </summary>
        /// <returns>string</returns>
        public override string GetFullText()
        {
            return base.TextUnit.Text;
        }

        /// <summary>
        /// Returns the rewritten text.
        /// </summary>
        /// <returns>string</returns>
        public override string GetRewrittenText()
        {
            return base.RewrittenTextUnit.Text;
        }

        #endregion

        #region internal API

        /// <summary>
        /// Rewrites the syntax node declaration to the intermediate C#
        /// representation.
        /// </summary>
        /// <param name="position">Position</param>
        internal override void Rewrite(ref int position)
        {
            base.RewrittenTextUnit = TextUnit.Clone(base.TextUnit, position);
            position = base.RewrittenTextUnit.End + 1;
        }

        /// <summary>
        /// Generates a new text unit.
        /// </summary>
        internal override void GenerateTextUnit()
        {
            this.TypeIdentifier.GenerateTextUnit();

            var text = "";

            if (this.Modifier != null)
            {
                text += this.Modifier.TextUnit.Text;
                text += " ";
            }

            text += this.TypeIdentifier.GetFullText();
            text += " ";

            text += this.Identifier.TextUnit.Text;

            text += this.SemicolonToken.TextUnit.Text + "\n";

            if (this.Modifier != null)
            {
                int length = this.SemicolonToken.TextUnit.End - this.Modifier.TextUnit.Start + 1;

                base.TextUnit = new TextUnit(text, length, this.Modifier.TextUnit.Start);
            }
            else
            {
                int length = this.SemicolonToken.TextUnit.End - this.TypeIdentifier.TextUnit.Start + 1;

                base.TextUnit = new TextUnit(text, length, this.TypeIdentifier.TextUnit.Start);
            }
        }

        #endregion
    }
}
