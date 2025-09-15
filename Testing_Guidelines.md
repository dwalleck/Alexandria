# Testing Guidelines

## Test Organization

- Group related tests in nested classes
- Use categories for test filtering
- Implement proper test isolation
- Use async/await consistently
- Follow AAA pattern (Arrange, Act, Assert)
- Use builder pattern to help create test data
- Use TUnit and Moq
- If we find a bug in our code, strongly consider writing a regression test
- Treat test code with the same care and thought you would application code
- Write clean tests. Use fixtures, injected data, and other capabilities to keep your test code clean, even if the setup is more complex
- Always take advantage of data driven testing. If any test cases fall into a pattern of input and expected output, make that data driven
- Use custom assertions when it makes sense. If there is a complicated assertion that needs to be performed more than 3 times, a custom assertion may be in order
