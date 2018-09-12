using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BlobCompiler
{
    public interface IParserFileAccess
    {
        Lexer OpenFileForLexing(string sourceFile, string path);
    }

    public class Parser
    {
        private Lexer m_CurrentLexer;

        private IParserFileAccess m_FileAccess;

        public Parser(IParserFileAccess fileAccess)
        {
            m_FileAccess = fileAccess;
        }

        public ParseResult Parse(string filename)
        {
            return Parse(filename, null);
        }

        private ParseResult Parse(string filename, string sourceFile)
        {
            var oldLexer = m_CurrentLexer;
            ParseResult result = null;
            try
            {
                using (var lexer = m_FileAccess.OpenFileForLexing(sourceFile, filename))
                {
                    m_CurrentLexer = lexer;
                    result = DoParse();
                }
            }
            finally
            {
                m_CurrentLexer = oldLexer;
            }
            return result;
        }

        private ParseResult DoParse()
        {
            var result = new ParseResult(m_CurrentLexer.Filename);

            while (!Accept(TokenType.EndOfFile))
            {
                if (Accept(TokenType.Struct))
                {
                    ParseStruct(result);
                }
                else if (Accept(TokenType.Constant))
                {
                    ParseConstant(result);
                }
                else if (Accept(TokenType.Include))
                {
                    ParseInclude(result);
                }
                else
                {
                    throw MakeParseError($"unexpected {m_CurrentLexer.Peek().SummaryWithoutLocation()} at file scope");
                }
            }

            return result;
        }

        private static int Priority(Token t, out BinaryExpressionType expressionType)
        {
            switch (t.Type)
            {
                case TokenType.LeftShift: expressionType = BinaryExpressionType.LeftShift; return 1;
                case TokenType.RightShift: expressionType = BinaryExpressionType.RightShift; return 1;
                case TokenType.Plus: expressionType = BinaryExpressionType.Add; return 2;
                case TokenType.Minus: expressionType = BinaryExpressionType.Sub; return 2;
                case TokenType.Star: expressionType = BinaryExpressionType.Mul; return 3;
                case TokenType.Slash: expressionType = BinaryExpressionType.Div; return 3;
            }

            // Dummy
            expressionType = BinaryExpressionType.Add;
            return 0;
        }

        /*
        private static bool IsRightAssociative(BinaryExpressionType et)
        {
            return false;
        }*/

        private void ParseConstant(ParseResult result)
        {
            var idToken = Expect(TokenType.Identifier);
            Expect(TokenType.Equal);
            var expr = ParseExpression(1);
            Accept(TokenType.SemiColon);

            result.Constants.Add(new ConstDef { Name = idToken.StringValue, Expression = expr, Location = idToken.Location });
        }

        private Expression ParseExpression(int precedence)
        {
            Expression p = ParseAtom();

            BinaryExpressionType et;

            for (; ; )
            {
                int pri = Priority(m_CurrentLexer.Peek(), out et);

                if (pri < precedence)
                    break;

                var token = m_CurrentLexer.Next();

                //int nextPri = IsRightAssociative(et) ? pri : pri + 1;
                int nextPri =  pri + 1;

                p = new BinaryExpression { ExpressionType = et, Left = p, Right = ParseExpression(nextPri) };
            }

            return p;
        }

        private Expression ParseAtom()
        {
            Token tok;

            if (Accept(TokenType.IntegerLiteral, out tok))
            {
                return new LiteralExpression { Location = tok.Location, Value = tok.IntValue };
            }
            else if (Accept(TokenType.Identifier, out tok))
            {
                return new IdentifierExpression { Location = tok.Location, Name = tok.StringValue };
            }
            else if (Accept(TokenType.LeftParen))
            {
                var expr = ParseExpression(1);
                Expect(TokenType.RightParen);
                return expr;
            }
            else if (Accept(TokenType.BitwiseNegate))
            {
                return new UnaryExpression { Location = tok.Location, Expression = ParseAtom(), ExpressionType = UnaryExpressionType.BitwiseNegate };
            }
            else if (Accept(TokenType.Minus))
            {
                return new UnaryExpression { Location = tok.Location, Expression = ParseAtom(), ExpressionType = UnaryExpressionType.Negate };
            }
            else
            {
                throw MakeParseError("expected atom");
            }
        }

        private ParseException MakeParseError(string error)
        {
            return new ParseException(m_CurrentLexer.Peek(), error);
        }

        private ParseException MakeParseError(Token tok, string error)
        {
            return new ParseException(tok, error);
        }

        private void ParseStruct(ParseResult result)
        {
            var name = Expect(TokenType.Identifier);

            Expect(TokenType.LeftBrace);

            result.Structs.Add(new StructDef
            {
                Location = name.Location,
                Name = name.StringValue,
                Fields = ParseStructFields(result)
            });
        }

        private List<FieldDef> ParseStructFields(ParseResult result)
        {
            List<FieldDef> fields = new List<FieldDef>();

            while (!Accept(TokenType.RightBrace))
            {
                ParseStructField(fields, result);
            }

            Accept(TokenType.SemiColon);
            return fields;
        }

        private void ParseStructField(List<FieldDef> outFields, ParseResult result)
        {
            TypeDef type = ParseType(true, result);

            do
            {
                var name = Expect(TokenType.Identifier);
                outFields.Add(new FieldDef
                {
                    Name = name.StringValue,
                    Location = name.Location,
                    Type = type
                });

            } while (Accept(TokenType.Comma));

            Expect(TokenType.SemiColon);
        }

        private TypeDef ParseType(bool allowFunctionTypes, ParseResult result)
        {
            TypeDef type = ParseBaseType();

            for (; ; )
            {
                Token t;

                if (Accept(TokenType.LeftBracket, out t))
                {
                    var bounds = Expect(TokenType.IntegerLiteral);
                    if (bounds.IntValue < 0)
                        throw MakeParseError(bounds, $"array bounds must be positive; got {bounds.IntValue}");
                    Expect(TokenType.RightBracket);
                    type = new ArrayType(t.Location, bounds.IntValue, type);
                }
                else if (Accept(TokenType.Star, out t))
                {
                    if (type is ArrayType)
                        throw MakeParseError(t, "cannot declare pointer to array type");

                    type = new PointerType(t.Location, type);
                }
                else if (Accept(TokenType.LeftParen, out t))
                {
                    var args = ParseFunctionArgs(result);
                    type = new FunctionType(t.Location, type, args);
                    result.FunctionTypes.Add((FunctionType)type);
                }
                else
                {
                    break;
                }
            }

            return type;
        }

        private List<FunctionArgument> ParseFunctionArgs(ParseResult result)
        {
            List<FunctionArgument> args = new List<FunctionArgument>();
            for (; ; )
            {
                if (Accept(TokenType.RightParen))
                    break;

                if (args.Count > 0)
                    Expect(TokenType.Comma);

                TypeDef argType = ParseType(false, result);
                var id = Expect(TokenType.Identifier);
                args.Add(new FunctionArgument { Name = id.StringValue, Type = argType });
            }

            return args;
        }

        private TypeDef ParseBaseType()
        {
            var token = m_CurrentLexer.Next();
            switch (token.Type)
            {
                case TokenType.U8: return PrimitiveType.U8;
                case TokenType.U16: return PrimitiveType.U16;
                case TokenType.U32: return PrimitiveType.U32;
                case TokenType.I8: return PrimitiveType.I8;
                case TokenType.I16: return PrimitiveType.I16;
                case TokenType.I32: return PrimitiveType.I32;
                case TokenType.F32: return PrimitiveType.F32;
                case TokenType.F64: return PrimitiveType.F64;
                case TokenType.Void: return VoidType.Instance;
                case TokenType.Identifier: return new StructType(token.Location, token.StringValue);
            }
            throw MakeParseError(token, $"expected type; got {token.Type}");
        }

        private void ParseInclude(ParseResult result)
        {
            var fn = Expect(TokenType.QuotedString);

            try
            {
                var nestedResult = Parse(fn.StringValue, fn.Location.Filename);
                foreach (StructDef def in nestedResult.Structs)
                {
                    def.WasIncluded = true;
                }
                foreach (ConstDef def in nestedResult.Constants)
                {
                    def.WasIncluded = true;
                }
                result.Structs.AddRange(nestedResult.Structs);
                result.Constants.AddRange(nestedResult.Constants);
                result.Includes.Add(fn.StringValue);
            }
            catch (IOException ex)
            {
                // TODO: Should keep nested exception
                throw MakeParseError(fn, $"file not found: '{fn.StringValue}' - {ex.Message}");
            }
        }

        private Token Expect(TokenType type)
        {
            var t = m_CurrentLexer.Next();
            if (t.Type != type)
            {
                throw MakeParseError(t, $"expected {type}, got {t.Type}");
            }
            return t;
        }

        private bool Accept(TokenType type)
        {
            if (m_CurrentLexer.Peek().Type == type)
            {
                m_CurrentLexer.Next();
                return true;
            }
            return false;
        }

        private bool Accept(TokenType type, out Token t)
        {
            if (m_CurrentLexer.Peek().Type == type)
            {
                t = m_CurrentLexer.Next();
                return true;
            }
            t = default(Token);
            return false;
        }

    }
}
