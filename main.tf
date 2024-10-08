provider "aws" {
  region  = "eu-north-1"
}

data "aws_iam_role" "lambda_exec_role" {
  name = "lambda_exec_getChats"  # Використовуйте існуючу роль
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
  filename         = "bin/release/net8.0/getChats.zip"
  function_name    = "getChats"
  role             = data.aws_iam_role.lambda_exec_role.arn
  handler          = "getChats::getChats.Function::FunctionHandler"
  runtime          = "dotnet8"
  memory_size      = 512
  timeout          = 30
  architectures    = ["x86_64"]
}

resource "aws_api_gateway_rest_api" "api_gateway" {
  name        = "gatChats"
  description = "API Gateway for getChats Lambda function"
}

resource "aws_api_gateway_resource" "lambda_resource" {
  rest_api_id = aws_api_gateway_rest_api.api_gateway.id
  parent_id   = aws_api_gateway_rest_api.api_gateway.root_resource_id
  path_part   = "chats"
}

resource "aws_api_gateway_method" "lambda_method" {
  rest_api_id   = aws_api_gateway_rest_api.api_gateway.id
  resource_id   = aws_api_gateway_resource.lambda_resource.id
  http_method   = "GET"
  authorization = "NONE"
}

resource "aws_lambda_permission" "api_gateway_permission" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.getChats.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.api_gateway.execution_arn}/*/*"
}

resource "aws_api_gateway_integration" "lambda_integration" {
  rest_api_id = aws_api_gateway_rest_api.api_gateway.id
  resource_id = aws_api_gateway_resource.lambda_resource.id
  http_method = aws_api_gateway_method.lambda_method.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.getChats.invoke_arn
}
