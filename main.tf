provider "aws" {
  region  = "eu-north-1"
}

data "aws_iam_role" "lambda_exec_role" {
  name = "lambda_exec_getChats"
}

resource "aws_iam_role_policy" "dynamodb_policy" {
  name   = "dynamodb_access"
  role   = data.aws_iam_role.lambda_exec_role.id
  policy = jsonencode({
    "Version": "2012-10-17",
    "Statement": [
      {
        "Action": [
          "dynamodb:Query",
          "dynamodb:Scan",
          "dynamodb:GetItem"
        ],
        "Effect": "Allow",
        "Resource": "*"
      }
    ]
  })
}

resource "aws_lambda_function" "getChats" {
  filename         = "bin/Release/net8.0/getChats.zip"
  function_name    = "getChats"
  role             = data.aws_iam_role.lambda_exec_role.arn
  handler          = "getChats::getChats.Function::FunctionHandler"
  runtime          = "dotnet8"
  memory_size      = 512
  timeout          = 30
  architectures    = ["x86_64"]

  source_code_hash = filebase64sha256("bin/Release/net8.0/getChats.zip")
}

