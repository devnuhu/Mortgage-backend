# Copilot Instructions

## Project Guidelines
- Add ADMIN role access to all endpoints where REVIEWER has access, but NOT LEAD_REVIEWER. ADMIN should have all the same document review and assignment viewing capabilities as REVIEWER but NOT the reassignment capability that only LEAD_REVIEWER has.

## Review Status Tracking
- The AccessMortgage system has TWO separate review status tracking mechanisms:
  - Application-level review status in the Applications table - tracks the overall application review state (draft, submitted, under_review, rejected, resubmitted, returned, approved, completed).
  - Document-level review status in the ApplicationDocuments table - tracks individual document review states (Pending, Approved, Rejected). These are independent concepts and should not be confused.

## RSA PIN Validation
- Update database context to make RsaPin unique with filter.
- Add validation methods to ApplicationService.
- Add validation endpoint to ApplicationsController.
- Update CreateApplicationAsync and SubmitApplicationAsync with RsaPin checks.