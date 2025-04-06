# Vibe coding experiment April 2025

The idea of this was to see how successful it would be to get AI to generate a full stack e-commerce application, including authentication and credit card processing, without me needing any programming knowledge to be able to implement the technologies.

 _Obviously there was a certain level of knowledge required to come up with the initial instructions, but no expertise was required to complete the implementation._

The model used for this was `Gemini 2.5 Pro Preview 03-25`.

I asked the model to generate all the required code and I simply copied and ran what it gave me then reported any errors I ran into.

The instructions I gave it at the start were:

> I would like you to design and give c# .net9 code using the latest best practices and patterns, with all components running as docker containers, that will do the following:
> 
> 1. Create a responsive and adaptive Blazor based tshirt store website that allows users to
> 
>    * anonymously browse items or search by item description
>    * sign up/sign in to the site using either a new email & password or their Microsoft or Google accounts, preferably include TOTP MFA support
>    * add items to a cart whether signed in or not
> 
> 2. Use KeyCloak for Identity and Access Management with data stored in a "keycloak" database in the postgres server
> 
> 3. Have the front end communicate with a c# minimal API to search for products and add to a cart. The API should be stateless and the cart should be maintained entirely on the browser side
> 
> 4. Require the user to be signed in to go to the checkout, or view their order history
> 
> 5. Use bearer token authentication when completing the check out process so that the API can identify the user and add to their order history which will be in an "order" database in the postgres server
> 
> 6. During checkout the user can pay by credit card with payments handled by Stripe. The front end will use Stripe Elements and the API will use the stripe/stripe-mock:latest docker container for payment processing
> 
> 7. Include support for client API access using apikey/secret as well as user authentication
> 
> 8. Have components use Serilog logging with a Seq sink
> 
> The implementation you provide must:
> 
> 1. Be ready to run with docker compose, with instructions included. Any initial configuration of keycloak should be in a configuration file supplied to the container.
> 
> 2. Work with http://localhost:<ports> as this is only for development, not production use, so HTTPS is not required
> 
> 3. Include robust error handling
> 
> 4. Include full unit tests in separate unit test projects, using NSubstitute, AutoFixture and Shouldly, where appropriate
> 
> 5. Include integration tests in separate integration test projects
> 
> 6. Seed the db with sample product data, image links should use https://picum.photos, provide EF migrations for the data and have the api ensure the db is created and up to date
> 
> 7. Support token refresh in Blazor
> 
> 8. Include a jenkinsfile to build and test the code
> 
> 9. Include a pgadmin image in the docker compose file, ideally pre-configured to connect to the server
> 
> 10. Include dockage/mailcatcher:0.9.0 in the docker compose file that can be used as the SMTP server for KeyCloak
> 


I then copied the contents of the files it generated and had some back and forth to resolve issues found with running the application.
I did not add any code or make any changes that were not given to me by the AI model.

The only deviations or manual interventions I did were:

1. Changed the docker compose file to use fixed versions of the docker images so that it would not break in future.
1. Generated a full keycloak config file by connecting to a running instance and manually creating the client settings and exporting, but I did this following instructions the model gave me. This replaced the basic sample config initially provided.


# Issues

I had to spend many hours with the model trying different things to resolve the numerous errors that I encountered building and running the application


# Conclusion

It took more than a full day's work to complete and at times was very frustrating to not intervene when the AI was struggling with a problem. 
The conversation used 400,000 tokens and required a lot of patience to persist with.

While it is far from perfect I would say that the end result was a success for the ability to use AI to create a usable application without the user needing any programming knowledge.

However one very important end result was the test quality:
```
Test summary: total: 42, failed: 36, succeeded: 6, skipped: 0, duration: 2.7s
```
Those results are totally unacceptable.

Was it faster to produce the end result than an experienced full stack developer working alone? I would say yes but not a lot faster, it took a lot longer than I had expected. That will probably be drastically different in another year from now.


# Running the solution

```
docker compose up -d --build
```

I never got around to checking whether the generated Jenkinsfile worked or not :)