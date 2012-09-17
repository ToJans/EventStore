#!/bin/bash

curl -i -X POST -d '

fromCategory("account").foreachStream().whenAny(
  function (state, ev) {
    if (state.c === undefined) state.c = 0;
    if (state.c != ev.sequenceNumber)
      throw "stream: " + ev.streamId + " state.c: " + state.c + " seq: " + ev.sequenceNumber;
    state.c++;
    return state;
  }
);     

' http://127.0.0.1:2113/projections/persistent?name=foreach%20acocunt%20stream\&type=JS

