# TourScanner
A quick application making it possible to scan tour operators' web pages and tracking price and available rooms movements.

When you plan to go for a trip and hope to get a good deal from your tour operator on some last rooms it is important to be able to track changes to price and rooms available, so you don't miss your chance. I've created this application so it could run in a container exposing an Api, that would return the changes since the last call to the caller and send them to a preconfigured email address (in case there were any).

This application was written specifically for one time usage with Polish ITAKA web site and yahoo SMTP mail server, so I didn't add any architecture beyond what is required, since there was not much time left anyway (I think it worked for, like, 2 days before we bought the tickets). I think I will generalize the approach at some point, when I will need to scan some other provider, for example. Until then - feel free to customize it to your liking.
