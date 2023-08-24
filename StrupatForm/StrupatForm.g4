grammar StrupatForm;

options {
	contextSuperClass = ParserRuleContextEx;
}

grammar_: rule_ (Newline+ rule_)* Newline? EOF;
rule_: Name alternatives;
alternatives: Space '->' Space alternative | (Newline Indentation alternative)+;
alternative: item (Space item)*;
item: (ruleRef | literal | class | '(' alternative ')') quantifier?;
ruleRef: Name;
literal: CharLiteral | StringLiteral;

quantifier: zeroOrOne | zeroOrMany | oneOrMany | exactly | atLeast | between;

zeroOrOne: '?';
zeroOrMany: '*';
oneOrMany: '+';
exactly: '{' Number '}';
atLeast: '{' min=Number ',' '}';
between: '{' min=Number ',' max=Number '}';

class
	: '[' not='^'? range+ ']'
	| range
	;

fragment Word: '\\w';
fragment Digit: '\\d';
fragment Whitespace: '\\s';
fragment NotWord: '\\W';
fragment NotDigit: '\\D';
fragment NotWhitespace: '\\S';
ShorthandCharacterClass: Word | Digit | Whitespace | NotWord | NotDigit | NotWhitespace;

//Custom: '[' ~[\]]+ ']';
Newline: '\r' | '\n' | '\r\n';
Indentation: '\t' | '    ';
CharLiteral: '\'' (EscapedCharacter | UnicodeEscapeCharacter | ~'\'') '\'';
StringLiteral: '"' (EscapedCharacter | UnicodeEscapeCharacter | ~'"')* '"';
fragment EscapedCharacter: '\\' [0\\tnr"'];
fragment UnicodeEscapeCharacter
	: '\\x{' Hexadec Hexadec? Hexadec? Hexadec? Hexadec? Hexadec? Hexadec? Hexadec? '}'
	| '\\u' Hexadec Hexadec Hexadec Hexadec
	| '\\U' Hexadec Hexadec Hexadec Hexadec Hexadec Hexadec Hexadec Hexadec
	;
fragment Hexadec: [0-9a-fA-F];
Name: [\p{Alpha}\p{General_Category=Other_Letter}] [\p{Alnum}\p{General_Category=Other_Letter}]*;
Space : ' ';
Number: [\p{Decimal_Number}]+;
Letter: [\p{Alpha}];

range: Character '-' Character | Character | ShorthandCharacterClass;
Character: Letter | Number | EscapedCharacter | ~( '[' | ']' | '-' | '\\' );
