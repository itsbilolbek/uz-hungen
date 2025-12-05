using System.Text;

namespace UzHunGen.Converter;

public enum TokenType
{
    SFX, PFX, TAG, END, CLASS, ONLYROOT,
    ENDSWITH, STARTSWITH, STRIP, NOT, 
    IDENTIFIER, STRING, NUMBER,
    LBRACE, RBRACE, LBRACKET, RBRACKET, LPAREN, RPAREN, 
    COMMA, COLON, PLUS, EQUAL, 
    NEWLINE, EOF
}

public record Token(TokenType Type, string Value, int Line, int Column, bool NewLine = false)
{
    public override string ToString() => $"{Type}: {Value} ({Line}:{Column})";
}


// Lekser
public class RuleLexer
{

    private string _input;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    // Kalit so'zlar
    private static readonly IReadOnlyDictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
    {
        ["SFX"] = TokenType.SFX,
        ["PFX"] = TokenType.PFX,
        ["TAG"] = TokenType.TAG,
        ["END"] = TokenType.END,
        ["CLASS"] = TokenType.CLASS,
        ["ONLYROOT"] = TokenType.ONLYROOT,
        ["ENDSWITH"] = TokenType.ENDSWITH,
        ["STARTSWITH"] = TokenType.STARTSWITH,
        ["STRIP"] = TokenType.STRIP
    };

    // Maxsus belgilar
    private static readonly IReadOnlyDictionary<char, TokenType> SingleCharTokens = new Dictionary<char, TokenType>
    {
        ['{'] = TokenType.LBRACE,
        ['}'] = TokenType.RBRACE,
        ['['] = TokenType.LBRACKET,
        [']'] = TokenType.RBRACKET,
        ['('] = TokenType.LPAREN,
        [')'] = TokenType.RPAREN,
        [','] = TokenType.COMMA,
        [':'] = TokenType.COLON,
        ['+'] = TokenType.PLUS,
        ['='] = TokenType.EQUAL
    };

    // Identifikator nomida ishlatish mumkin bo'lgan belgimi?
    private static bool IsValidIdentifierSymbol(char c) => char.IsLetterOrDigit(c) || "-_‘’'?!.".Contains(c);

    public RuleLexer(string input)
    {
        _input = input;
    }

    // Tokenlarga ajratish
    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_position < _input.Length)
        {
            var token = NextToken();

            if (token is not null) tokens.Add(token);
        }

        tokens.Add(new Token(TokenType.EOF, string.Empty, _line, _column));

        return tokens;
    }

    private char CurrentChar() => _input[_position];

    // Keyingi token
    private Token? NextToken()
    {
        var startLine = _line;
        var startColumn = _column;
    
        // Izohlar
        SkipWhitespace();

        // Maxsus belgilar
        if (SingleCharTokens.TryGetValue(CurrentChar(), out var tokenType))
        {
            _position++;
            _column++;
            return new Token(tokenType, CurrentChar().ToString(), startLine, startColumn, SkipWhitespace());
        }
        
        // Mantli literallar
        if (CurrentChar() == '"')
            return ReadString();

        // Identifikator va kalit so'zlar va sonlar
        if (IsValidIdentifierSymbol(CurrentChar()))
            return ReadIdentifier();

        throw new InvalidOperationException($"Notog'ri belgi '{CurrentChar()}', qator nomeri => {_line}:{_column}");
    }

    // Matnli literalni aniqlash
    private Token ReadString()
    {
        var startLine = _line;
        var startColumn = _column;
        var sb = new StringBuilder();

        _position++; // Ochuvchi qo'shtirnoqni tashlab yuborish
        _column++;

        while (_position < _input.Length && _input[_position] != '"')
        {
            if (_input[_position] == '\\' && _position + 1 < _input.Length)
            {
                _position++;
                _column++;
                sb.Append(_input[_position]);
            }
            else
            {
                sb.Append(_input[_position]);
            }

            if (_input[_position] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }

            _position++;
        }

        if (_position >= _input.Length)
            throw new InvalidOperationException($"Yopilmagan matnli literal, qator nomeri => {startLine}:{startColumn}");

        _position++; // Yopuvchi qo'shtirnoqni tashlab yuborish
        _column++;

        return new Token(TokenType.STRING, sb.ToString(), startLine, startColumn, SkipWhitespace());
    }



    // Identifikator va kalit so'zlarni aniqlash
    private Token ReadIdentifier()
    {
        var startLine = _line;
        var startColumn = _column;
        var sb = new StringBuilder();
       
        while (_position < _input.Length && IsValidIdentifierSymbol(_input[_position]))
        {
            sb.Append(_input[_position]);
            _position++;
            _column++;
        }

        var value = sb.ToString();

        // Agar son bo'lsa
        if (int.TryParse(value, out _))
            return new Token(TokenType.NUMBER, value, startLine, startColumn, SkipWhitespace());

        // Kalit so'z yoki identifikator
        var type = Keywords.TryGetValue(value.ToUpperInvariant(), out var keywordType)
            ? keywordType
            : TokenType.IDENTIFIER;
        
        return new Token(type, value, startLine, startColumn, SkipWhitespace());
    }

    // Bo'sh joylarni tashlab o'tish
    // Yangi qatorlar bo'lsa true qaytaradi
    private bool SkipWhitespace()
    {
        var newLine = false;

        while(true)
        {
            while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            {
                if (_input[_position] == '\n')
                {
                    newLine = true;
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
                _position++;
            }

            if (_position >= _input.Length) break;

            if (_input[_position] != '#') break;

            SkipComment();
        }

        return newLine;
    }

    // Izohlarni tashlab o'tish
    private void SkipComment()
    {
        while (_position < _input.Length && _input[_position] != '\n')
        {
            _position++;
            _column++;
        }
    }
}
