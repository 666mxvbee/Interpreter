package main

const (
	minInteger      = -2147483648
	maxInteger      = 2147483647
	maxNestingDepth = 512
)

type Expr interface{ isExpr() }

type Var struct{ Name string }
type Const struct{ Value int }
type Binop struct {
	Op          string
	Left, Right Expr
}

func (Var) isExpr()   {}
func (Const) isExpr() {}
func (Binop) isExpr() {}

type String struct{ Value string }
type Array struct{ Elements []Expr }
type Element struct{ Source, Index Expr }
type Length struct{ Source Expr }
type SExpression struct {
	Name      string
	Arguments []Expr
}

func (String) isExpr()      {}
func (Array) isExpr()       {}
func (Element) isExpr()     {}
func (Length) isExpr()      {}
func (SExpression) isExpr() {}

type Stmt interface{ isStmt() }

type Read struct{ Name string }
type Write struct{ Expr Expr }
type Assign struct {
	Name string
	Expr Expr
}
type Seq struct {
	First, Second Stmt
}
type While struct {
	Cond Expr
	Body Stmt
}
type DoWhile struct {
	Body Stmt
	Cond Expr
}
type If struct {
	Cond Expr
	Then Stmt
	Else Stmt
}
type Skip struct{}
type Set struct{ Destination, Index, Value Expr }

func (Set) isStmt() {}

func (Read) isStmt()    {}
func (Write) isStmt()   {}
func (Assign) isStmt()  {}
func (Seq) isStmt()     {}
func (While) isStmt()   {}
func (DoWhile) isStmt() {}
func (If) isStmt()      {}
func (Skip) isStmt()    {}
