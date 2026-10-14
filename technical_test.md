# Technical Test

## Objective

Build an ELT backend service that periodically imports fuel mix data from the public MISO real-time data API, loads it in an AWS RDS PostgreSQL database, and provides a safe way for an external user to view the ingested data.

This assessment is intended to evaluate backend engineering, cloud engineering, DevOps practices, PostgreSQL design, observability, secure configuration, and operational thinking.


Source API: 
```text
https://public-api.misoenergy.org/api/FuelMix
```

MISO has a rate limit for their public API.


## Expected Deliverables
1. A GitHub repository with your solution, including:
   * Source code
   * Infrastructure-as-Code
   * Documentation of your design and implementation decisions, and instructions for running the service
2. Evidence that your solution works
3. Access to the AWS account where your solution is deployed, if possible


## Requirements

### 1. Tech stack and deployment
Preferred backend languages and frameworks:
* **.NET** with ASP.NET Core, Entity Framework, xUnit, etc.
* **Python** with FastAPI, SQLAlchemy, pytest, etc.


For this assessment, an AWS deployment is preferred but not strictly required if the design is documented and the service can be run manually.
You can create a free-tier AWS account here: https://aws.amazon.com/free/

### 2. Data ingestion

The ingestion should be executable by at least one of the following:

* CLI command
* PGAgent job
* ECS scheduled task
* Lambda scheduled by EventBridge


### 3. PostgreSQL

Use a PostgreSQL cluster as the target rdbms.
Choose either a star schema or a normalized design, as appropriate.
Your ingestion must be idempotent. Running the same import multiple times should not create duplicate tuples.
Your data model should support tracking, and efficient querying.


### 4. Reporting

Provide a safe way for an external user to view the ingested data.
Do not expose a privileged database user. Do not expose the instance publicly to the world.



### 5. Alerting and monitoring

Implement monitoring and alerting appropriate for a small production service.


### 6. Infrastructure and deployment

Your service infrastructure should be:

* Containerized
* Idempotently deployable
* Managed with Infrastructure-as-Code

Use one of:

* Terraform
* AWS CDK
* CloudFormation

Deployment should be repeatable. Running the deployment multiple times should update existing resources safely rather than requiring manual recreation.
