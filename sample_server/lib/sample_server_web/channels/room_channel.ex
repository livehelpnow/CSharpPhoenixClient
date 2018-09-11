defmodule SampleServerWeb.RoomChannel do
  use Phoenix.Channel
  require Logger

  def join("room:lobby", %{"user_id" => user_id}, socket) do
    {:ok, %{response: "Hi #{user_id}, your are in lobby channel now"}, socket}
  end

  def join("room:" <> _private_room_id, _params, _socket) do
    {:error, %{reason: "unauthorized"}}
  end

  def handle_in("new_msg", %{"body" => body}, socket) do
    broadcast!(socket, "new_msg", %{body: body})
    {:reply, {:ok, %{message: "sent"}}, socket}
  end
end
