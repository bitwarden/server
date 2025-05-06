# External vendors
group "Payment Systems" {
  stripe = softwareSystem "Stripe" {
    tags "External"
    tags "Billing"
    description "Handles credit cards and subscriptions."
  }
  braintree = softwareSystem "Braintree" {
    tags "External"
    tags "Billing"
    description "Handles PayPal and cryptocurrency."
  }
}

