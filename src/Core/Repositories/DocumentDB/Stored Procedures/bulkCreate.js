/**
* This script called as stored procedure to import lots of documents in one batch.
* The script sets response body to the number of docs imported and is called multiple times 
* by the client until total number of docs desired by the client is imported.
* @param  {Object[]} docs - Array of documents to import.
*/

function bulkCreate(docs) {
    var collection = getContext().getCollection();
    var collectionLink = collection.getSelfLink();

    // The count of imported docs, also used as current doc index.
    var count = 0;

    // Validate input.
    if (!docs) throw new Error('The array is undefined or null.');

    var docsLength = docs.length;
    if (docsLength == 0) {
        getContext().getResponse().setBody(0);
        return;
    }

    // Call the CRUD API to create a document.
    tryCreate(docs[count], callback);

    // Note that there are 2 exit conditions:
    // 1) The createDocument request was not accepted. 
    //    In this case the callback will not be called, we just call setBody and we are done.
    // 2) The callback was called docs.length times.
    //    In this case all documents were created and we don't need to call tryCreate anymore. Just call setBody and we are done.
    function tryCreate(doc, callback) {
        var isAccepted = collection.createDocument(collectionLink, doc, callback);

        // If the request was accepted, callback will be called.
        // Otherwise report current count back to the client, 
        // which will call the script again with remaining set of docs.
        // This condition will happen when this stored procedure has been running too long
        // and is about to get cancelled by the server. This will allow the calling client
        // to resume this batch from the point we got to before isAccepted was set to false
        if (!isAccepted) getContext().getResponse().setBody(count);
    }

    // This is called when collection.createDocument is done and the document has been persisted.
    function callback(err, doc, options) {
        if (err) throw err;

        // One more document has been inserted, increment the count.
        count++;

        if (count >= docsLength) {
            // If we have created all documents, we are done. Just set the response.
            getContext().getResponse().setBody(count);
        }
        else {
            // Create next document.
            tryCreate(docs[count], callback);
        }
    }
}
