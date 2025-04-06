pipeline {
    agent any // Or specify a node with .NET SDK and Docker installed

    environment {
        // Define environment variables if needed, e.g., for integration test connection strings
        // TEST_DB_CONNECTION_STRING = '...'
        DOTNET_SDK = 'net9.0' // Specify SDK version if multiple are installed
    }

    stages {
        stage('Checkout') {
            steps {
                echo 'Checking out code...'
                checkout scm
            }
        }

        stage('Restore & Build') {
            steps {
                echo "Restoring and Building Solution..."
                // Use sh for Linux/macOS agents, bat for Windows
                sh """
                    dotnet restore TShirtStore.sln
                    dotnet build TShirtStore.sln --configuration Release --no-restore
                """
                /* For Windows agent:
                bat 'dotnet restore TShirtStore.sln'
                bat 'dotnet build TShirtStore.sln --configuration Release --no-restore'
                */
            }
        }

        stage('Unit Tests') {
            steps {
                echo "Running Unit Tests..."
                sh 'dotnet test TShirtStore.sln --configuration Release --no-build --filter "TestCategory=Unit" --logger "trx;LogFileName=unit_test_results.trx"'
                /* For Windows agent:
                bat 'dotnet test TShirtStore.sln --configuration Release --no-build --filter "TestCategory=Unit" --logger "trx;LogFileName=unit_test_results.trx"'
                */
            }
            post {
                always {
                    junit '**/unit_test_results.trx' // Publish test results
                }
            }
        }

        // --- Optional: Integration Tests (Require Docker Environment) ---
        // This stage assumes Docker and Docker Compose are available on the agent
        // and the Jenkins user has permissions to run docker commands.
        stage('Integration Tests') {
             when { expression { return fileExists('docker-compose.yml') } } // Only run if compose file exists
             environment {
                // Define env vars needed by docker-compose.yml for tests if any
                // e.g., specific ports or configurations for test environment
             }
            steps {
                 script {
                    try {
                        echo "Starting services for Integration Tests..."
                        // Use a specific compose override file for testing if needed
                        sh 'docker compose -f docker-compose.yml up -d postgres_db keycloak stripe_mock seq tshirtstore_api' // Start dependencies + API

                        // Wait for services to be ready (implement robust checks if needed)
                        echo "Waiting for services..."
                        sh 'sleep 30' // Simple wait, replace with proper health checks

                        echo "Running Integration Tests..."
                        // Assume integration tests are marked with a category or live in a specific project
                        // Adjust the filter or project path as needed
                        sh 'dotnet test tests/TShirtStore.IntegrationTests/TShirtStore.IntegrationTests.csproj --configuration Release --no-build --filter "TestCategory=Integration" --logger "trx;LogFileName=integration_test_results.trx"'

                    } catch (Exception e) {
                        echo "Integration Test stage failed: ${e.message}"
                        throw e // Re-throw to mark the stage as failed
                    } finally {
                        echo "Stopping Integration Test services..."
                        // Stop and remove containers defined in the compose file
                         sh 'docker compose -f docker-compose.yml down -v --remove-orphans'
                    }
                 }
            }
             post {
                always {
                    junit '**/integration_test_results.trx' // Publish test results
                }
             }
        }

         // --- Optional: Build Docker Images ---
         stage('Build Docker Images') {
             steps {
                 echo "Building Docker images..."
                 sh 'docker compose -f docker-compose.yml build tshirtstore_api'
                 sh 'docker compose -f docker-compose.yml build tshirtstore_blazorapp'
                 // Add docker login and push steps here if deploying to a registry
                 // Example:
                 // withCredentials([usernamePassword(credentialsId: 'dockerhub-creds', usernameVariable: 'DOCKER_USERNAME', passwordVariable: 'DOCKER_PASSWORD')]) {
                 //     sh 'echo $DOCKER_PASSWORD | docker login -u $DOCKER_USERNAME --password-stdin'
                 //     sh 'docker tag tshirtstore_api your-dockerhub-username/tshirtstore_api:latest'
                 //     sh 'docker push your-dockerhub-username/tshirtstore_api:latest'
                 //     // Repeat for blazorapp
                 // }
             }
         }
    }

    post {
        always {
            echo 'Pipeline finished.'
            // Clean up workspace, etc.
            // cleanWs()
        }
        success {
            echo 'Pipeline succeeded!'
            // Notify success
        }
        failure {
            echo 'Pipeline failed!'
            // Notify failure (e.g., email, Slack)
            // mail to: '...', subject: "Jenkins Pipeline Failed: ${env.JOB_NAME} [${env.BUILD_NUMBER}]", body: "..."
        }
    }
}